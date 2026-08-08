using System.Collections.Generic;
using FishNet.Object;
using Game.Core.Items;
using Game.Presentation.Abilities;
using Game.Presentation.Combat;
using Game.Presentation.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Game.Presentation.UI
{
    public class InventoryUIController : NetworkBehaviour
    {
        [SerializeField] private ItemDatabase _database;
        [SerializeField] private RunInventory _inventory;
        [SerializeField] private PlayerMovementController _movement;
        [SerializeField] private CameraLookController _cameraLook;
        [SerializeField] private AbilityController _abilities;
        [SerializeField] private PlayerInteraction _interaction;
        [SerializeField] private UIDocument _document;

        private VisualElement _root;
        private VisualElement _invPanel; // panel interior — para detectar drag fuera
        private VisualElement _equipmentSlots;
        private VisualElement _backpackGrid;
        private VisualElement _containerColumn;
        private VisualElement _containerGrid;

        private InputAction _toggleAction;
        private bool _isOpen;

        private LootContainer _openContainer;

        // ---------- Drag & drop ----------
        private enum SlotZone { Equipment, Backpack, Container }
        private struct DragInfo { public SlotZone Zone; public int Index; public ItemStack Stack; }
        private DragInfo _dragging;
        private bool _isDragging;
        private VisualElement _ghost;
        private bool _dragMoved;
        private bool _dragOutside; // true cuando el puntero está fuera del inv-panel

        private static readonly Color GhostColorNormal  = new Color(0.16f, 0.16f, 0.24f, 0.95f);
        private static readonly Color GhostColorDrop    = new Color(0.55f, 0.08f, 0.08f, 0.95f);

        private void Awake()
        {
            _toggleAction = new InputAction("ToggleInventory", InputActionType.Button, "<Keyboard>/tab");
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!base.IsOwner) { enabled = false; return; }

            var go = GameObject.Find("InventoryUIDocument");
            if (go != null) _document = go.GetComponent<UIDocument>();

            if (_document == null)
            {
                Debug.LogError("[InventoryUI] No se encontró 'InventoryUIDocument' en la escena.");
                return;
            }

            _root         = _document.rootVisualElement.Q<VisualElement>("inv-root");
            _invPanel     = _root.Q<VisualElement>("inv-panel");
            _equipmentSlots = _root.Q<VisualElement>("equipment-slots");
            _backpackGrid   = _root.Q<VisualElement>("backpack-grid");
            _containerColumn = _root.Q<VisualElement>("container-column");
            _containerGrid   = _root.Q<VisualElement>("container-grid");

            _inventory.OnInventoryChanged += Redraw;
            _toggleAction.Enable();

            // Soltar fuera de un slot: drop al mundo si se arrastró, cancelar si no.
            _root.RegisterCallback<PointerUpEvent>(OnRootPointerUp);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (_inventory != null) _inventory.OnInventoryChanged -= Redraw;
            _toggleAction.Disable();
        }

        private void Update()
        {
            if (!base.IsOwner) return;
            if (_toggleAction.WasPressedThisFrame()) SetOpen(!_isOpen);
        }

        private void SetOpen(bool open)
        {
            _isOpen = open;
            _root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;

            UnityEngine.Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            UnityEngine.Cursor.visible = open;
            if (_cameraLook != null)  _cameraLook.enabled = !open;
            if (_movement != null)    _movement.SetInputBlocked(open);
            if (_abilities != null)   _abilities.SetInputBlocked(open);
            if (_interaction != null) _interaction.SetInputBlocked(open);

            if (open) Redraw();
            else CloseContainer();
        }

        public void OpenWithContainer(LootContainer container)
        {
            _openContainer = container;
            if (container != null) container.RegisterChangeCallback(Redraw);
            SetOpen(true);
        }

        private void CloseContainer()
        {
            if (_openContainer != null) _openContainer.UnregisterChangeCallback(Redraw);
            _openContainer = null;
        }

        public void DisableInventory()
        {
            if (_isOpen) SetOpen(false);
            _toggleAction.Disable();
            enabled = false;
        }

        // ---------- Dibujo ----------

        private void Redraw()
        {
            if (!_isOpen) return;
            DrawEquipment();
            DrawBackpack();
            DrawContainer();
        }

        private void DrawEquipment()
        {
            _equipmentSlots.Clear();
            var equip = _inventory.Equipment;
            for (int i = 0; i < equip.Count; i++)
            {
                int slotIndex = i;
                var slot = new VisualElement();
                slot.AddToClassList("equip-slot");

                var label = new Label(((EquipmentSlot)i).ToString());
                label.AddToClassList("equip-slot-label");
                slot.Add(label);

                ItemStack stack = equip[i];
                if (!stack.IsEmpty)
                {
                    ItemSO def = _database.GetById(stack.ItemId);
                    var name = new Label(def != null ? def.DisplayName : stack.ItemId);
                    name.AddToClassList("item-name");
                    ApplyCategoryClass(slot, def);
                    slot.Add(name);

                    slot.RegisterCallback<ClickEvent>(evt =>
                    {
                        if (_dragMoved) { _dragMoved = false; return; }
                        if (evt.ctrlKey) { DropItemServerRpc((int)SlotZone.Equipment, slotIndex); return; }
                        UnequipServerRpc(slotIndex);
                    });

                    slot.RegisterCallback<PointerDownEvent>(evt =>
                    {
                        if (evt.button != 0) return;
                        BeginDrag(SlotZone.Equipment, slotIndex, stack, evt.position);
                    });
                }

                slot.RegisterCallback<PointerUpEvent>(_ => TryDrop(SlotZone.Equipment, slotIndex));
                _equipmentSlots.Add(slot);
            }
        }

        private void DrawBackpack()
        {
            _backpackGrid.Clear();
            var bp = _inventory.Backpack;
            for (int i = 0; i < bp.Count; i++)
            {
                int slotIndex = i;
                ItemStack stack = bp[i];

                // Si es equipo, el clic lo manda al slot correcto vía TryMoveSlot (soporta swap de mochila).
                // Si no es equipo, EquipServerRpc igual falla — no hay acción de clic para no-equipo.
                System.Action onClick = null;
                if (!stack.IsEmpty && _database.GetById(stack.ItemId) is EquipmentItemSO equip)
                    onClick = () => QuickEquipServerRpc(1, slotIndex);

                _backpackGrid.Add(BuildItemSlot(stack, SlotZone.Backpack, slotIndex, onClick));
            }
        }

        private void DrawContainer()
        {
            if (_openContainer == null)
            {
                _containerColumn.style.display = DisplayStyle.None;
                return;
            }

            _containerColumn.style.display = DisplayStyle.Flex;
            _containerGrid.Clear();

            var contents = _openContainer.Contents;
            for (int i = 0; i < contents.Count; i++)
            {
                int idx = i;
                _containerGrid.Add(BuildItemSlot(contents[i], SlotZone.Container, idx,
                    () => TakeFromContainerServerRpc(_openContainer, idx)));
            }
        }

        private VisualElement BuildItemSlot(ItemStack stack, SlotZone zone, int index, System.Action onClick)
        {
            var slot = new VisualElement();
            slot.AddToClassList("item-slot");

            if (!stack.IsEmpty)
            {
                ItemSO def = _database.GetById(stack.ItemId);
                ApplyCategoryClass(slot, def);

                var name = new Label(def != null ? def.DisplayName : stack.ItemId);
                name.AddToClassList("item-name");
                slot.Add(name);

                if (stack.Quantity > 1)
                {
                    var qty = new Label($"x{stack.Quantity}");
                    qty.AddToClassList("item-qty");
                    slot.Add(qty);
                }

                slot.RegisterCallback<ClickEvent>(evt =>
                {
                    if (_dragMoved) { _dragMoved = false; return; }
                    if (evt.ctrlKey)  { DropItemServerRpc((int)zone, index); return; }
                    if (evt.shiftKey) { QuickEquipServerRpc((int)zone, index); return; }
                    onClick?.Invoke();
                });

                slot.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    BeginDrag(zone, index, stack, evt.position);
                });
            }

            slot.RegisterCallback<PointerUpEvent>(_ => TryDrop(zone, index));
            return slot;
        }

        private void ApplyCategoryClass(VisualElement slot, ItemSO def)
        {
            if (def == null) return;
            switch (def.Category)
            {
                case ItemCategory.Material:   slot.AddToClassList("cat-material");   break;
                case ItemCategory.Resource:   slot.AddToClassList("cat-resource");   break;
                case ItemCategory.Equipment:  slot.AddToClassList("cat-equipment");  break;
                case ItemCategory.Catalyst:   slot.AddToClassList("cat-catalyst");   break;
                case ItemCategory.Consumable: slot.AddToClassList("cat-consumable"); break;
                default:                      slot.AddToClassList("cat-misc");       break;
            }
        }

        // ---------- Drag & drop ----------

        private void BeginDrag(SlotZone zone, int index, ItemStack stack, Vector2 pos)
        {
            _dragging  = new DragInfo { Zone = zone, Index = index, Stack = stack };
            _isDragging = true;
            _dragMoved  = false;
            _dragOutside = false;

            _ghost = new VisualElement();
            _ghost.AddToClassList("drag-ghost");
            _ghost.pickingMode = PickingMode.Ignore;
            var def = _database.GetById(stack.ItemId);
            var name = new Label(def != null ? def.DisplayName : stack.ItemId);
            name.AddToClassList("item-name");
            name.pickingMode = PickingMode.Ignore;
            _ghost.Add(name);
            _root.Add(_ghost);
            MoveGhost(pos);

            _root.RegisterCallback<PointerMoveEvent>(OnDragMove);
        }

        private void OnDragMove(PointerMoveEvent evt)
        {
            if (!_isDragging) return;
            _dragMoved = true;
            MoveGhost(evt.position);

            // Detectar si el puntero está fuera del inv-panel y cambiar color del ghost.
            bool outside = _invPanel != null && !_invPanel.worldBound.Contains(evt.position);
            if (outside != _dragOutside)
            {
                _dragOutside = outside;
                _ghost.style.backgroundColor = outside ? GhostColorDrop : GhostColorNormal;
            }
        }

        private void MoveGhost(Vector2 pos)
        {
            if (_ghost == null) return;
            _ghost.style.left = pos.x - 28;
            _ghost.style.top  = pos.y - 28;
        }

        private void TryDrop(SlotZone destZone, int destIndex)
        {
            if (!_isDragging) return;

            var from  = _dragging;
            bool moved = _dragMoved;
            EndDrag();

            if (!moved) return;

            // No se puede depositar en el contenedor, solo sacar de él.
            if (destZone == SlotZone.Container) return;

            if (from.Zone == SlotZone.Container)
            {
                if (_openContainer == null) return;
                MoveWithContainerServerRpc((int)from.Zone, from.Index, (int)destZone, destIndex, _openContainer);
            }
            else
            {
                MoveSlotServerRpc((int)from.Zone, from.Index, (int)destZone, destIndex);
            }
        }

        // Soltar en el root (fuera de cualquier slot): drop al mundo si se arrastró.
        private void OnRootPointerUp(PointerUpEvent evt)
        {
            if (!_isDragging) return;

            var from  = _dragging;
            bool moved = _dragMoved;
            EndDrag();

            if (!moved) return; // clic suelto fuera de slot, no arrastre

            // Items del contenedor no se dropean al mundo desde acá (solo se pueden sacar a slots).
            if (from.Zone == SlotZone.Container) return;

            DropToWorldServerRpc((int)from.Zone, from.Index);
        }

        private void CancelDrag() => EndDrag();

        private void EndDrag()
        {
            _isDragging  = false;
            _dragOutside = false;
            _root.UnregisterCallback<PointerMoveEvent>(OnDragMove);
            if (_ghost != null) { _ghost.RemoveFromHierarchy(); _ghost = null; }
        }

        // ---------- Acciones (server-authoritative) ----------

        [ServerRpc] private void EquipServerRpc(int backpackIndex) => _inventory.TryEquipFromBackpack(backpackIndex);
        [ServerRpc] private void UnequipServerRpc(int equipmentSlotIndex) => _inventory.TryUnequip(equipmentSlotIndex);

        [ServerRpc]
        private void TakeFromContainerServerRpc(LootContainer container, int index)
        {
            if (container == null) return;
            if (!container.ServerTryTake(index, out ItemStack taken)) return;

            int notAdded = _inventory.TryAddStack(taken);
            if (notAdded > 0)
                container.ServerFill(new[] { new ItemStack(taken.ItemId, notAdded, taken.Durability) });
        }

        [ServerRpc]
        private void MoveSlotServerRpc(int fromZone, int fromIndex, int toZone, int toIndex)
            => _inventory.TryMoveSlot(fromZone, fromIndex, toZone, toIndex);

        [ServerRpc]
        private void MoveWithContainerServerRpc(int fromZone, int fromIndex, int toZone, int toIndex, LootContainer container)
            => _inventory.TryMoveWithContainer(fromZone, fromIndex, toZone, toIndex, container);

        [ServerRpc]
        private void DropToWorldServerRpc(int zone, int index)
            => _inventory.TryDropToWorld(zone, index, transform.position);

        [ServerRpc]
        private void DropItemServerRpc(int zone, int index)
            => _inventory.TryDropToWorld(zone, index, transform.position);

        [ServerRpc]
        private void QuickEquipServerRpc(int zone, int index)
        {
            ItemStack stack = zone == 1 ? _inventory.Backpack[index] : ItemStack.Empty;
            if (stack.IsEmpty) return;
            if (_database.GetById(stack.ItemId) is not EquipmentItemSO equip) return;
            _inventory.TryMoveSlot(zone, index, 0, (int)equip.Slot);
        }


    }
}