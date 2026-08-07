using Game.Core.Items;
using Game.Presentation.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation.UI
{
    /// <summary>
    /// Pantalla de gestión en el menú: inventario propio (equipo + mochila) y stash lado a lado.
    /// Trabaja sobre datos planos (snapshot del PlayerLoadoutService + StashData del StashService),
    /// sin red. Clic mueve items entre inventario y stash (y equipa/desequipa).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class StashScreenController : MonoBehaviour
    {
        [SerializeField] private ItemDatabase _database;
        [SerializeField] private StartingKitSO _startingKit; // para inicializar si nunca jugó

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _equipmentSlots;
        private VisualElement _backpackGrid;
        private VisualElement _stashGrid;

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            _root = _document.rootVisualElement.Q<VisualElement>("stash-root");
            _equipmentSlots = _root.Q<VisualElement>("equipment-slots");
            _backpackGrid = _root.Q<VisualElement>("backpack-grid");
            _stashGrid = _root.Q<VisualElement>("stash-grid");

            var closeBtn = _root.Q<Button>("stash-close");
            if (closeBtn != null) closeBtn.clicked += Hide;
        }

        public void Show()
        {
            // Asegurar que el inventario propio exista (kit inicial la primera vez).
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
        private void DrawEquipment()
        {
            _equipmentSlots.Clear();
            var equip = Inv.Equipment;

            // El snapshot guarda un stack por cada slot de equipo, en orden del enum.
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
                    ApplyCategoryClass(slot, def);
                    var name = new Label(def != null ? def.DisplayName : stack.ItemId);
                    name.AddToClassList("item-name");
                    slot.Add(name);

                    // Clic en equipo: desequipar → va a la mochila.
                    slot.RegisterCallback<ClickEvent>(_ => UnequipToBackpack(slotIndex));
                }

                _equipmentSlots.Add(slot);
            }
        }

        // ---------- Mochila ----------
        private void DrawBackpack()
        {
            _backpackGrid.Clear();
            var bp = Inv.Backpack;
            for (int i = 0; i < bp.Count; i++)
            {
                int idx = i;
                _backpackGrid.Add(BuildItemSlotWithShift(bp[i],
                    normalClick: () => MoveBackpackToStash(idx),
                    shiftClick: () => EquipFromBackpack(idx)));
            }
            // Slots vacíos visuales para que se vea la grilla (hasta cierto número).
            // Opcional: mostrar solo los ocupados. Acá mostramos los ocupados nada más.
        }

        // ---------- Stash ----------
        private void DrawStash()
        {
            _stashGrid.Clear();
            var slots = Stash.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                int idx = i;
                _stashGrid.Add(BuildItemSlotWithShift(slots[i],
                    normalClick: () => MoveStashToInventory(idx),
                    shiftClick: () => EquipFromStash(idx)));
            }
        }

        private VisualElement BuildItemSlotWithShift(ItemStack stack, System.Action normalClick, System.Action shiftClick)
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
                    if (evt.shiftKey) shiftClick?.Invoke();
                    else normalClick?.Invoke();
                });
            }

            return slot;
        }


        private void EquipFromBackpack(int backpackIndex)
        {
            if (backpackIndex < 0 || backpackIndex >= Inv.Backpack.Count) return;
            ItemStack stack = Inv.Backpack[backpackIndex];
            TryEquip(stack, () => Inv.Backpack.RemoveAt(backpackIndex));
            Redraw();
        }

        private void EquipFromStash(int stashIndex)
        {
            ItemStack stack = Stash.Slots[stashIndex];
            TryEquip(stack, () => Stash.TakeAt(stashIndex));
            Redraw();
        }

        // ---------- Construcción de slot ----------
        private VisualElement BuildItemSlot(ItemStack stack, System.Action onClick)
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

                if (onClick != null)
                    slot.RegisterCallback<ClickEvent>(_ => onClick());
            }

            return slot;
        }

        // ---------- Operaciones de movimiento ----------

        /// <summary>Equipa un item equipable en su slot correcto. Si el slot está ocupado, intercambia.</summary>
        private bool TryEquip(ItemStack stack, System.Action removeFromSource)
        {
            if (stack.IsEmpty) return false;

            // Solo equipable si su definición es equipamiento.
            if (_database.GetById(stack.ItemId) is not EquipmentItemSO equip) return false;

            int slotIndex = (int)equip.Slot;
            if (slotIndex < 0 || slotIndex >= Inv.Equipment.Count) return false;

            ItemStack current = Inv.Equipment[slotIndex];

            // Sacar el item de su origen (mochila o stash).
            removeFromSource();

            // Equipar el nuevo.
            Inv.Equipment[slotIndex] = new ItemStack(stack.ItemId, 1, stack.Durability);

            // Si había algo equipado, va a la mochila (intercambio).
            if (!current.IsEmpty)
                Inv.Backpack.Add(current);

            return true;
        }


        // Mochila → Stash
        private void MoveBackpackToStash(int backpackIndex)
        {
            if (backpackIndex < 0 || backpackIndex >= Inv.Backpack.Count) return;
            ItemStack stack = Inv.Backpack[backpackIndex];
            if (stack.IsEmpty) return;

            int notAdded = Stash.Add(stack, id => _database.GetById(id));
            if (notAdded <= 0)
                Inv.Backpack.RemoveAt(backpackIndex);
            else
            {
                // Entró parte: actualizar la cantidad restante en la mochila.
                var s = stack; s.Quantity = notAdded; Inv.Backpack[backpackIndex] = s;
            }
            Redraw();
        }

        // Stash → Inventario (a la mochila)
        private void MoveStashToInventory(int stashIndex)
        {
            ItemStack stack = Stash.Slots[stashIndex];
            if (stack.IsEmpty) return;

            // Agregar a la mochila del inventario propio (lista dinámica).
            Inv.Backpack.Add(stack);
            Stash.TakeAt(stashIndex);
            Redraw();
        }

        // Equipo → Mochila (desequipar)
        private void UnequipToBackpack(int equipSlotIndex)
        {
            if (equipSlotIndex < 0 || equipSlotIndex >= Inv.Equipment.Count) return;
            ItemStack stack = Inv.Equipment[equipSlotIndex];
            if (stack.IsEmpty) return;

            Inv.Backpack.Add(stack);
            Inv.Equipment[equipSlotIndex] = ItemStack.Empty;
            Redraw();
        }

        private void ApplyCategoryClass(VisualElement slot, ItemSO def)
        {
            if (def == null) { slot.AddToClassList("cat-misc"); return; }
            switch (def.Category)
            {
                case ItemCategory.Material: slot.AddToClassList("cat-material"); break;
                case ItemCategory.Resource: slot.AddToClassList("cat-resource"); break;
                case ItemCategory.Equipment: slot.AddToClassList("cat-equipment"); break;
                case ItemCategory.Catalyst: slot.AddToClassList("cat-catalyst"); break;
                case ItemCategory.Consumable: slot.AddToClassList("cat-consumable"); break;
                default: slot.AddToClassList("cat-misc"); break;
            }
        }
    }
}