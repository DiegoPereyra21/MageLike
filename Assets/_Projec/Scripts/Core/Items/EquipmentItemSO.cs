using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.Items
{
    /// <summary>
    /// Item equipable: ocupa un slot de equipo y aporta modificadores de stat mientras está puesto.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Items/Equipment", fileName = "Equip_")]
    public class EquipmentItemSO : ItemSO
    {
        [Header("Equipamiento")]
        [SerializeField] private EquipmentSlot _slot;
        [SerializeField] private List<StatModifier> _modifiers = new List<StatModifier>();

        [Header("Mochila (solo si el slot es Backpack)")]
        [SerializeField, Min(0)] private int _backpackSlots = 0;

        public EquipmentSlot Slot => _slot;
        public IReadOnlyList<StatModifier> Modifiers => _modifiers;

        /// <summary>Cuántos slots de mochila aporta (solo relevante si Slot == Backpack).</summary>
        public int BackpackSlots => _backpackSlots;

        public override bool IsEquipment => true;
    }
}