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
    /// <summary>
    /// Pantalla de inventario (Tab). Dibuja equipo + mochila (+ contenedor si se está saqueando)
    /// leyendo del RunInventory sincronizado. Clic izquierdo = acción principal contextual
    /// (equipar / desequipar / tomar). Drag & drop = mover a slot exacto (con swap/merge).
    /// Las acciones se piden por ServerRpc; el servidor valida.
    /// </summary>
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

        private void Awake()
        {
            _toggleAction = new InputAction("ToggleInventory", InputActionType.Button, "<Keyboard>/tab");
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!base.IsOwner)
            {
                enabled = false;
                return;
            }

            var go = GameObject.Find("InventoryUIDocument");
            if (go != null) _document = go.GetComponent<UIDocument>();

            if (_document == null)
            {
                Debug.LogError("[InventoryUI] No se encontró 'InventoryUIDocument' en la escena.");
                return;
            }

            _root = _document.rootVisualElement.Q<VisualElement>("inv-root");
            _equipmentSlots = _root.Q<VisualElement>("equipment-slots");
            _backpackGrid = _root.Q<VisualElement>("backpack-grid");
            _containerColumn = _root.Q<VisualElement>("container-column");
            _containerGrid = _root.Q<VisualElement>("container-grid");

            _inventory.OnInventoryChanged += Redraw;
            _toggleAction.Enable();

            // Si se suelta fuera de un slot, cancelar el arrastre.
            _root.RegisterCallback<PointerUpEvent>(_ => { if (_isDragging) CancelDrag(); });
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
            if (_toggleAction.WasPressedThisFrame())
                SetOpen(!_isOpen);
        }

        private void SetOpen(bool open)
        {
            _isOpen = open;
            _root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;

            UnityEngine.Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            UnityEngine.Cursor.visible = open;
            if (_cameraLook != null) _cameraLook.enabled = !open;
            if (_movement != null) _movement.SetInputBlocked(open);
            if (_abilities != null) _abilities.SetInputBlocked(open);
            if (_interaction != null) _interaction.SetInputBlocked(open);

            if (open) Redraw();
            else CloseContainer();
        }

        public void OpenWithContainer(LootContainer container)
        {
            _openContainer = container;
            if (container != null)
                container.RegisterChangeCallback(Redraw);
            SetOpen(true);
        }

        private void CloseContainer()
        {
            if (_openContainer != null)
                _openContainer.UnregisterChangeCallback(Redraw);
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

                    slot.RegisterCallback<ClickEvent>(_ =>
                    {
                        if (_dragMoved) { _dragMoved = false; return; }
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
                _backpackGrid.Add(BuildItemSlot(bp[i], SlotZone.Backpack, slotIndex,
                    () => EquipServerRpc(slotIndex)));
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

                slot.RegisterCallback<ClickEvent>(_ =>
                {
                    if (_dragMoved) { _dragMoved = false; return; }
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
            _dragging = new DragInfo { Zone = zone, Index = index, Stack = stack };
            _isDragging = true;
            _dragMoved = false;

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
        }

        private void MoveGhost(Vector2 pos)
        {
            if (_ghost == null) return;
            _ghost.style.left = pos.x - 28;
            _ghost.style.top = pos.y - 28;
        }

        private void TryDrop(SlotZone destZone, int destIndex)
        {
            if (!_isDragging) return;

            var from = _dragging;
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


[ServerRpc]
        private void MoveSlotServerRpc(int fromZone, int fromIndex, int toZone, int toIndex)
            => _inventory.TryMoveSlot(fromZone, fromIndex, toZone, toIndex);

        private void CancelDrag() => EndDrag();

        private void EndDrag()
        {
            _isDragging = false;
            _root.UnregisterCallback<PointerMoveEvent>(OnDragMove);
            if (_ghost != null) { _ghost.RemoveFromHierarchy(); _ghost = null; }
        }

        // ---------- Acciones (server-authoritative) ----------

        [ServerRpc] private void EquipServerRpc(int backpackIndex) => _inventory.TryEquipFromBackpack(backpackIndex);
        [ServerRpc] private void UnequipServerRpc(int equipmentSlotIndex) => _inventory.TryUnequip(equipmentSlotIndex);


[ServerRpc]
        private void MoveWithContainerServerRpc(int fromZone, int fromIndex, int toZone, int toIndex, LootContainer container)
            => _inventory.TryMoveWithContainer(fromZone, fromIndex, toZone, toIndex, container);

        [ServerRpc]
        private void TakeFromContainerServerRpc(LootContainer container, int index)
        {
            if (container == null) return;
            if (!container.ServerTryTake(index, out ItemStack taken)) return;

            int notAdded = _inventory.TryAddStack(taken);
            if (notAdded > 0)
                container.ServerFill(new[] { new ItemStack(taken.ItemId, notAdded, taken.Durability) });
        }
    }
}