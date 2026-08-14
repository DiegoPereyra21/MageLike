using Game.Core.Items;
using Game.Presentation.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation.UI
{
    /// <summary>
    /// Pantalla de gestión en el menú: inventario propio (equipo + mochila) y stash lado a lado.
    /// Trabaja sobre datos planos (snapshot del PlayerLoadoutService + StashData del StashService),
    /// sin red. Clic mueve items; shift+clic equipa; arrastrar (drag & drop) mueve entre slots.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class StashScreenController : MonoBehaviour
    {
        [SerializeField] private ItemDatabase _database;
        [SerializeField] private StartingKitSO _startingKit;

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _equipmentSlots;
        private VisualElement _backpackGrid;
        private VisualElement _stashGrid;
        private Label _backpackLabel;
        private Label _stashLabel;

        private enum SlotZone { Equipment, Backpack, Stash }

        private struct DragInfo
        {
            public SlotZone Zone;
            public int Index;
            public ItemStack Stack;
        }

        private DragInfo _dragging;
        private bool _isDragging;
        private VisualElement _ghost;
        private bool _dragMoved;

        private const int FallbackBackpackSlots = 8;

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            _root = _document.rootVisualElement.Q<VisualElement>("stash-root");
            _equipmentSlots = _root.Q<VisualElement>("equipment-slots");
            _backpackGrid = _root.Q<VisualElement>("backpack-grid");
            _stashGrid = _root.Q<VisualElement>("stash-grid");
            _backpackLabel = _root.Q<Label>("backpack-label");
            _stashLabel = _root.Q<Label>("stash-label");

            var closeBtn = _root.Q<Button>("stash-close");
            if (closeBtn != null) closeBtn.clicked += Hide;

            // Limpieza del drag si se suelta en cualquier lado (fuera de un slot).
            _root.RegisterCallback<PointerUpEvent>(_ => { if (_isDragging) CancelDrag(); });
        }

        public void Show()
        {
            PlayerLoadoutService.EnsureInitialized(_startingKit);
            _root.style.display = DisplayStyle.Flex;
            Redraw();
        }

        public void Hide() => _root.style.display = DisplayStyle.None;

        private InventorySnapshot Inv => PlayerLoadoutService.Current;
        private StashData Stash => StashService.Stash;

        private void Redraw()
        {
            DrawEquipment();
            DrawBackpack();
            DrawStash();
        }

        // ---------- Equipamiento ----------
        // ---------- Equipamiento ----------
        private void DrawEquipment()
        {
            _equipmentSlots.Clear();
            var equip = Inv.Equipment;

            for (int i = 0; i < equip.Count; i++)
            {
                if ((EquipmentSlot)i == EquipmentSlot.Pants) continue; // oculto (pendiente sacarlo del código)

                int slotIndex = i;
                var row = new VisualElement();
                row.AddToClassList("equip-row");
                if (slotIndex == equip.Count - 1) row.AddToClassList("no-border"); // última fila real
                row.userData = new DragInfo { Zone = SlotZone.Equipment, Index = slotIndex, Stack = equip[i] };

                var label = new Label(((EquipmentSlot)i).ToString());
                label.AddToClassList("equip-row-label");
                row.Add(label);

                ItemStack stack = equip[i];
                if (!stack.IsEmpty)
                {
                    ItemSO def = _database.GetById(stack.ItemId);

                    var itemWrap = new VisualElement();
                    itemWrap.AddToClassList("equip-row-item");

                    var dot = new VisualElement();
                    dot.AddToClassList("accent-dot");
                    dot.AddToClassList(GetAccentClass(def));
                    itemWrap.Add(dot);

                    var name = new Label(def != null ? def.DisplayName : stack.ItemId);
                    name.AddToClassList("equip-item-name");
                    itemWrap.Add(name);

                    row.Add(itemWrap);

                    row.RegisterCallback<ClickEvent>(_ =>
                    {
                        if (_dragMoved) { _dragMoved = false; return; }
                        UnequipToBackpack(slotIndex);
                    });

                    row.RegisterCallback<PointerDownEvent>(evt =>
                    {
                        if (evt.button != 0) return;
                        BeginDrag(SlotZone.Equipment, slotIndex, stack, evt.position);
                    });
                }
                else
                {
                    var empty = new Label("— empty —");
                    empty.AddToClassList("equip-row-empty");
                    row.Add(empty);
                }

                row.RegisterCallback<PointerUpEvent>(_ => TryDrop(SlotZone.Equipment, slotIndex));
                _equipmentSlots.Add(row);
            }
        }

        // ---------- Mochila ----------
        private void DrawBackpack()
        {
            NormalizeBackpack();
            _backpackGrid.Clear();
            var bp = Inv.Backpack;
            if (_backpackLabel != null) _backpackLabel.text = $"Backpack — {bp.Count} Slots";

            for (int i = 0; i < bp.Count; i++)
            {
                int idx = i;
                _backpackGrid.Add(BuildItemSlot(bp[i], SlotZone.Backpack, idx,
                    normalClick: () => MoveBackpackToStash(idx),
                    shiftClick: () => EquipFromBackpack(idx)));
            }
        }

        private int BackpackCapacity()
        {
            int backpackSlotIndex = (int)EquipmentSlot.Backpack;
            if (backpackSlotIndex < Inv.Equipment.Count)
            {
                ItemStack bp = Inv.Equipment[backpackSlotIndex];
                if (!bp.IsEmpty && _database.GetById(bp.ItemId) is EquipmentItemSO e && e.Slot == EquipmentSlot.Backpack)
                    return Mathf.Max(e.BackpackSlots, 0);
            }
            return FallbackBackpackSlots;
        }

        private void NormalizeBackpack()
        {
            int cap = BackpackCapacity();
            var bp = Inv.Backpack;
            while (bp.Count < cap) bp.Add(ItemStack.Empty);
            while (bp.Count > cap && bp.Count > 0 && bp[bp.Count - 1].IsEmpty)
                bp.RemoveAt(bp.Count - 1);
        }

        // ---------- Stash ----------
        // ---------- Stash ----------
        private void DrawStash()
        {
            _stashGrid.Clear();
            var slots = Stash.Slots;
            if (_stashLabel != null) _stashLabel.text = $"Stash — {slots.Count} Slots";

            for (int i = 0; i < slots.Count; i++)
            {
                int idx = i;
                _stashGrid.Add(BuildItemSlot(slots[i], SlotZone.Stash, idx,
                    normalClick: () => MoveStashToInventory(idx),
                    shiftClick: () => EquipFromStash(idx)));
            }
        }

        // ---------- Construcción de slot (mochila / stash) ----------
        private VisualElement BuildItemSlot(ItemStack stack, SlotZone zone, int index,
            System.Action normalClick, System.Action shiftClick)
        {
            var slot = new VisualElement();
            slot.AddToClassList("item-slot");
            slot.userData = new DragInfo { Zone = zone, Index = index, Stack = stack };

            if (!stack.IsEmpty)
            {
                ItemSO def = _database.GetById(stack.ItemId);
                slot.AddToClassList(GetAccentClass(def));

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
                    if (evt.shiftKey) shiftClick?.Invoke();
                    else normalClick?.Invoke();
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

            if (!moved) return; // fue un clic, no un arrastre

            MoveItem(from.Zone, from.Index, destZone, destIndex);
            Redraw();
        }

        private void CancelDrag()
        {
            // Se soltó fuera de un slot: limpiar sin mover.
            EndDrag();
        }

        private void EndDrag()
        {
            _isDragging = false;
            _root.UnregisterCallback<PointerMoveEvent>(OnDragMove);
            if (_ghost != null) { _ghost.RemoveFromHierarchy(); _ghost = null; }
        }

        private void MoveItem(SlotZone fromZone, int fromIndex, SlotZone toZone, int toIndex)
        {
            if (fromZone == toZone && fromIndex == toIndex) return;

            ItemStack item = GetStack(fromZone, fromIndex);
            if (item.IsEmpty) return;

            // Si el destino es slot de equipo, validar que el item corresponda.
            if (toZone == SlotZone.Equipment)
            {
                if (_database.GetById(item.ItemId) is not EquipmentItemSO equip) return;
                if ((int)equip.Slot != toIndex) return;
            }

            ItemStack existing = GetStack(toZone, toIndex);

            SetStack(toZone, toIndex, new ItemStack(item.ItemId, item.Quantity, item.Durability));
            SetStack(fromZone, fromIndex, ItemStack.Empty);

            // Swap: lo que había en destino vuelve al origen.
            if (!existing.IsEmpty)
                SetStack(fromZone, fromIndex, existing);
        }

        private ItemStack GetStack(SlotZone zone, int index)
        {
            switch (zone)
            {
                case SlotZone.Equipment: return (index >= 0 && index < Inv.Equipment.Count) ? Inv.Equipment[index] : ItemStack.Empty;
                case SlotZone.Backpack:  return (index >= 0 && index < Inv.Backpack.Count)  ? Inv.Backpack[index]  : ItemStack.Empty;
                case SlotZone.Stash:     return (index >= 0 && index < Stash.Slots.Count)   ? Stash.Slots[index]   : ItemStack.Empty;
            }
            return ItemStack.Empty;
        }

        private void SetStack(SlotZone zone, int index, ItemStack stack)
        {
            switch (zone)
            {
                case SlotZone.Equipment: if (index >= 0 && index < Inv.Equipment.Count) Inv.Equipment[index] = stack; break;
                case SlotZone.Backpack:  if (index >= 0 && index < Inv.Backpack.Count)  Inv.Backpack[index]  = stack; break;
                case SlotZone.Stash:     if (index >= 0 && index < Stash.Slots.Count)   Stash.Slots[index]   = stack; break;
            }
        }

        // ---------- Equipar (shift+clic) ----------
        private void EquipFromBackpack(int backpackIndex)
        {
            if (backpackIndex < 0 || backpackIndex >= Inv.Backpack.Count) return;
            ItemStack stack = Inv.Backpack[backpackIndex];
            TryEquip(stack, () => Inv.Backpack[backpackIndex] = ItemStack.Empty);
            Redraw();
        }

        private void EquipFromStash(int stashIndex)
        {
            ItemStack stack = Stash.Slots[stashIndex];
            TryEquip(stack, () => Stash.TakeAt(stashIndex));
            Redraw();
        }

        private bool TryEquip(ItemStack stack, System.Action removeFromSource)
        {
            if (stack.IsEmpty) return false;
            if (_database.GetById(stack.ItemId) is not EquipmentItemSO equip) return false;

            int slotIndex = (int)equip.Slot;
            if (slotIndex < 0 || slotIndex >= Inv.Equipment.Count) return false;

            ItemStack current = Inv.Equipment[slotIndex];
            removeFromSource();
            Inv.Equipment[slotIndex] = new ItemStack(stack.ItemId, 1, stack.Durability);

            if (!current.IsEmpty)
                AddToBackpack(current);

            return true;
        }

        // ---------- Movimientos por clic ----------
        private void MoveBackpackToStash(int backpackIndex)
        {
            if (backpackIndex < 0 || backpackIndex >= Inv.Backpack.Count) return;
            ItemStack stack = Inv.Backpack[backpackIndex];
            if (stack.IsEmpty) return;

            int notAdded = Stash.Add(stack, id => _database.GetById(id));
            if (notAdded <= 0)
                Inv.Backpack[backpackIndex] = ItemStack.Empty;
            else
            {
                var s = stack; s.Quantity = notAdded; Inv.Backpack[backpackIndex] = s;
            }
            Redraw();
        }

        private void MoveStashToInventory(int stashIndex)
        {
            ItemStack stack = Stash.Slots[stashIndex];
            if (stack.IsEmpty) return;

            AddToBackpack(stack);
            Stash.TakeAt(stashIndex);
            Redraw();
        }

        private void UnequipToBackpack(int equipSlotIndex)
        {
            if (equipSlotIndex < 0 || equipSlotIndex >= Inv.Equipment.Count) return;
            ItemStack stack = Inv.Equipment[equipSlotIndex];
            if (stack.IsEmpty) return;

            AddToBackpack(stack);
            Inv.Equipment[equipSlotIndex] = ItemStack.Empty;
            Redraw();
        }

        private bool AddToBackpack(ItemStack stack)
        {
            var bp = Inv.Backpack;
            for (int i = 0; i < bp.Count; i++)
            {
                if (bp[i].IsEmpty) { bp[i] = stack; return true; }
            }
            return false;
        }

        /// <summary>
        /// Acento de color: por slot específico si es equipo (verde=movilidad/Boots,
        /// cian=utilidad/Hat, violeta=protección/Robe, dorado=Catalyst, ámbar=Backpack);
        /// genérico rojizo para todo lo demás (recursos, materiales, consumibles).
        /// </summary>
        private string GetAccentClass(ItemSO def)
        {
            if (def is EquipmentItemSO equip)
            {
                switch (equip.Slot)
                {
                    case EquipmentSlot.Boots: return "accent-green";
                    case EquipmentSlot.Hat: return "accent-cyan";
                    case EquipmentSlot.Robe: return "accent-violet";
                    case EquipmentSlot.Catalyst: return "accent-gold";
                    case EquipmentSlot.Backpack: return "accent-amber";
                }
            }
            return "accent-loot";
        }
    }
}