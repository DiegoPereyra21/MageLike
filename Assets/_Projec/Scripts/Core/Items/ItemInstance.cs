using System;
using System.Collections.Generic;

namespace Game.Core.Items
{
    /// <summary>
    /// Instancia concreta de un item en runtime. Comparte definición (ItemSO) con otras
    /// instancias del mismo tipo, pero tiene estado propio: cantidad, durabilidad y, si es
    /// una mochila, su contenido interno. Es lo que se guarda en inventarios y se sincroniza.
    /// </summary>
    [Serializable]
    public class ItemInstance
    {
        public ItemSO Definition;
        public int Quantity = 1;
        public float Durability = 1f; // 0..1; placeholder para desgaste futuro

        /// <summary>
        /// Contenido interno (solo si la definición es una mochila). Permite que una mochila
        /// tirada arrastre sus items. Null / vacío para items normales.
        /// </summary>
        public List<ItemInstance> Contents;

        public ItemInstance(ItemSO definition, int quantity = 1)
        {
            Definition = definition;
            Quantity = quantity;

            // Si es mochila, inicializa su contenedor interno con la capacidad definida.
            if (definition is EquipmentItemSO equip && equip.Slot == EquipmentSlot.Backpack && equip.BackpackSlots > 0)
                Contents = new List<ItemInstance>();
        }

        public bool IsStackable => Definition != null && Definition.IsStackable;
        public int MaxStack => Definition != null ? Definition.MaxStack : 1;
        public bool IsBackpack => Definition is EquipmentItemSO e && e.Slot == EquipmentSlot.Backpack;

        /// <summary>Espacio libre en esta pila (0 si no es apilable o está llena).</summary>
        public int RemainingStackSpace => IsStackable ? Math.Max(0, MaxStack - Quantity) : 0;
    }
}