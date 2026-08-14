using Game.Core.Items;
using Game.Presentation.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation.UI
{
    /// <summary>
    /// Pantalla de gestión en el menú: inventario propio (equipo + dos pockets) y stash lado a
    /// lado. Trabaja sobre datos planos (snapshot del PlayerLoadoutService + StashData del
    /// StashService), sin red. Clic mueve items; shift+clic equipa; arrastrar mueve entre slots.
    /// Cada pocket siempre muestra 12 casilleros; los que superan la capacidad actual del pocket
    /// equipado quedan bloqueados (oscuros, sin interacción) hasta equipar uno más grande.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class StashScreenController : MonoBehaviour
    {
        [SerializeField] private ItemDatabase _database;
        [SerializeField] private StartingKitSO _startingKit;

        private const int MaxPocketSlots = 12;
        private const int DefaultPocketCapacity = 1;

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _equipmentSlots;
        private VisualElement _pocketLGrid;
        private VisualElement _pocketRGrid;
        private Label _pocketLLabel;
        private Label _pocketRLabel;
        private VisualElement _stashGrid;
        private Label _stashLabel;

        private enum SlotZone { Equipment, PocketL, PocketR, Stash }

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

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            _root = _document.rootVisualElement.Q<VisualElement>("stash-root");
            _equipmentSlots = _root.Q<VisualElement>("equipment-slots");
            _pocketLGrid = _root.Q<VisualElement>("pocket-l-grid");
            _pocketRGrid = _root.Q<VisualElement>("pocket-r-grid");
            _pocketLLabel = _root.Q<Label>("pocket-l-label");
            _pocketRLabel = _root.Q<Label>("pocket-r-label");
            _stashGrid = _root.Q<VisualElement>("stash-grid");
            _stashLabel = _root.Q<Label>("stash-label");

            var closeBtn = _root.Q<Button>("stash-close");
            if (closeBtn != null) closeBtn.clicked += Hide;

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
            DrawPockets();
            DrawStash();
        }

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
                row.AddToClassList("equip-slot");
                if (slotIndex == equip.Count - 1) row.AddToClassList("no-border");
                row.userData = new DragInfo { Zone = SlotZone.Equipment, Index = slotIndex, Stack = equip[i] };

                var label = new Label(DisplayNameForSlot((EquipmentSlot)i));
                label.AddToClassList("equip-slot-label");
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
                        UnequipToPocket(slotIndex);
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

        private string DisplayNameForSlot(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.PocketL => "Pocket L",
                EquipmentSlot.PocketR => "Pocket R",
                _ => slot.ToString()
            };
        }

        // ---------- Pockets ----------
        private void DrawPockets()
        {
            NormalizePocket(Inv.PocketL, PocketCapacity(EquipmentSlot.PocketL));
            NormalizePocket(Inv.PocketR, PocketCapacity(EquipmentSlot.PocketR));

            DrawPocketGrid(_pocketLGrid, _pocketLLabel, "Pocket L", Inv.PocketL, SlotZone.PocketL);
            DrawPocketGrid(_pocketRGrid, _pocketRLabel, "Pocket R", Inv.PocketR, SlotZone.PocketR);
        }

        private void DrawPocketGrid(VisualElement grid, Label label, string title, System.Collections.Generic.List<ItemStack> list, SlotZone zone)
        {
            grid.Clear();
            if (label != null) label.text = $"{title} — {list.Count}/{MaxPocketSlots}";

            for (int i = 0; i < MaxPocketSlots; i++)
            {
                if (i < list.Count)
                {
                    int idx = i;
                    grid.Add(BuildItemSlot(list[i], zone, idx,
                        normalClick: () => MovePocketToStash(zone, idx),
                        shiftClick: () => EquipFromPocket(zone, idx)));
                }
                else
                {
                    var locked = new VisualElement();
                    locked.AddToClassList("item-slot");
                    locked.AddToClassList("locked");
                    grid.Add(locked);
                }
            }
        }

        private int PocketCapacity(EquipmentSlot pocketSlot)
        {
            int idx = (int)pocketSlot;
            if (idx < Inv.Equipment.Count)
            {
                ItemStack eq = Inv.Equipment[idx];
                if (!eq.IsEmpty && _database.GetById(eq.ItemId) is EquipmentItemSO e && e.Slot.IsPocket())
                    return Mathf.Clamp(e.PocketSlots, 0, MaxPocketSlots);
            }
            return DefaultPocketCapacity;
        }

        private void NormalizePocket(System.Collections.Generic.List<ItemStack> list, int cap)
        {
            while (list.Count < cap) list.Add(ItemStack.Empty);
            while (list.Count > cap && list.Count > 0 && list[list.Count - 1].IsEmpty)
                list.RemoveAt(list.Count - 1);
        }

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

        // ---------- Construcción de slot ----------
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

        /// <summary>Acento por slot si es equipo (verde=Boots, cian=Hat, violeta=Robe, dorado=Catalyst/Pocket), rojizo genérico si no.</summary>
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
                    case EquipmentSlot.PocketL:
                    case EquipmentSlot.PocketR:
                        return "accent-amber";
                }
            }
            return "accent-loot";
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
            _ghost.style.left = pos.x - 30;
            _ghost.style.top = pos.y - 30;
        }

        private void TryDrop(SlotZone destZone, int destIndex)
        {
            if (!_isDragging) return;

            var from = _dragging;
            bool moved = _dragMoved;
            EndDrag();

            if (!moved) return;

            MoveItem(from.Zone, from.Index, destZone, destIndex);
            Redraw();
        }

        private void CancelDrag() => EndDrag();

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

            if (toZone == SlotZone.Equipment)
            {
                if (_database.GetById(item.ItemId) is not EquipmentItemSO equip) return;
                if (!ValidEquipTarget(equip, toIndex)) return;
            }

            ItemStack existing = GetStack(toZone, toIndex);

            SetStack(toZone, toIndex, new ItemStack(item.ItemId, item.Quantity, item.Durability));
            SetStack(fromZone, fromIndex, ItemStack.Empty);

            if (!existing.IsEmpty)
                SetStack(fromZone, fromIndex, existing);
        }

        /// <summary>Pocket-aware: un ítem de pocket entra en cualquiera de las dos posiciones (L o R).</summary>
        private bool ValidEquipTarget(EquipmentItemSO equip, int toIndex)
        {
            if (equip.Slot.IsPocket())
                return toIndex == (int)EquipmentSlot.PocketL || toIndex == (int)EquipmentSlot.PocketR;
            return (int)equip.Slot == toIndex;
        }

        private ItemStack GetStack(SlotZone zone, int index)
        {
            switch (zone)
            {
                case SlotZone.Equipment: return (index >= 0 && index < Inv.Equipment.Count) ? Inv.Equipment[index] : ItemStack.Empty;
                case SlotZone.PocketL: return (index >= 0 && index < Inv.PocketL.Count) ? Inv.PocketL[index] : ItemStack.Empty;
                case SlotZone.PocketR: return (index >= 0 && index < Inv.PocketR.Count) ? Inv.PocketR[index] : ItemStack.Empty;
                case SlotZone.Stash: return (index >= 0 && index < Stash.Slots.Count) ? Stash.Slots[index] : ItemStack.Empty;
            }
            return ItemStack.Empty;
        }

        private void SetStack(SlotZone zone, int index, ItemStack stack)
        {
            switch (zone)
            {
                case SlotZone.Equipment: if (index >= 0 && index < Inv.Equipment.Count) Inv.Equipment[index] = stack; break;
                case SlotZone.PocketL: if (index >= 0 && index < Inv.PocketL.Count) Inv.PocketL[index] = stack; break;
                case SlotZone.PocketR: if (index >= 0 && index < Inv.PocketR.Count) Inv.PocketR[index] = stack; break;
                case SlotZone.Stash: if (index >= 0 && index < Stash.Slots.Count) Stash.Slots[index] = stack; break;
            }
        }

        // ---------- Equipar (shift+clic) ----------
        private void EquipFromPocket(SlotZone zone, int index)
        {
            var list = zone == SlotZone.PocketL ? Inv.PocketL : Inv.PocketR;
            if (index < 0 || index >= list.Count) return;
            ItemStack stack = list[index];
            TryEquip(stack, () => list[index] = ItemStack.Empty);
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

            int slotIndex;
            if (equip.Slot.IsPocket())
            {
                int lIdx = (int)EquipmentSlot.PocketL;
                int rIdx = (int)EquipmentSlot.PocketR;
                if (Inv.Equipment[lIdx].IsEmpty) slotIndex = lIdx;
                else if (Inv.Equipment[rIdx].IsEmpty) slotIndex = rIdx;
                else slotIndex = lIdx; // las dos ocupadas: reemplaza L
            }
            else
            {
                slotIndex = (int)equip.Slot;
            }

            if (slotIndex < 0 || slotIndex >= Inv.Equipment.Count) return false;

            ItemStack current = Inv.Equipment[slotIndex];
            removeFromSource();
            Inv.Equipment[slotIndex] = new ItemStack(stack.ItemId, 1, stack.Durability);

            if (!current.IsEmpty)
                AddToPockets(current);

            return true;
        }

        // ---------- Movimientos por clic ----------
        private void MovePocketToStash(SlotZone zone, int index)
        {
            var list = zone == SlotZone.PocketL ? Inv.PocketL : Inv.PocketR;
            if (index < 0 || index >= list.Count) return;
            ItemStack stack = list[index];
            if (stack.IsEmpty) return;

            int notAdded = Stash.Add(stack, id => _database.GetById(id));
            if (notAdded <= 0)
                list[index] = ItemStack.Empty;
            else
            {
                var s = stack; s.Quantity = notAdded; list[index] = s;
            }
            Redraw();
        }

        private void MoveStashToInventory(int stashIndex)
        {
            ItemStack stack = Stash.Slots[stashIndex];
            if (stack.IsEmpty) return;

            AddToPockets(stack);
            Stash.TakeAt(stashIndex);
            Redraw();
        }

        private void UnequipToPocket(int equipSlotIndex)
        {
            if (equipSlotIndex < 0 || equipSlotIndex >= Inv.Equipment.Count) return;
            ItemStack stack = Inv.Equipment[equipSlotIndex];
            if (stack.IsEmpty) return;

            AddToPockets(stack);
            Inv.Equipment[equipSlotIndex] = ItemStack.Empty;
            Redraw();
        }

        /// <summary>Primer slot libre entre los dos pockets (L primero).</summary>
        private bool AddToPockets(ItemStack stack)
        {
            for (int i = 0; i < Inv.PocketL.Count; i++)
                if (Inv.PocketL[i].IsEmpty) { Inv.PocketL[i] = stack; return true; }
            for (int i = 0; i < Inv.PocketR.Count; i++)
                if (Inv.PocketR[i].IsEmpty) { Inv.PocketR[i] = stack; return true; }
            return false;
        }
    }
}