using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.Items
{
    /// <summary>
    /// Kit de principiante: el equipo e items con los que arranca un jugador nuevo
    /// (inventario propio inicial la primera vez). Configurable en el Editor.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Items/Starting Kit", fileName = "StartingKit")]
    public class StartingKitSO : ScriptableObject
    {
        [System.Serializable]
        public struct EquipEntry
        {
            public EquipmentSlot Slot;
            public EquipmentItemSO Item;
        }

        [System.Serializable]
        public struct BackpackEntry
        {
            public ItemSO Item;
            [Min(1)] public int Quantity;
        }

        [SerializeField] private List<EquipEntry> _equipment = new List<EquipEntry>();
        [SerializeField] private List<BackpackEntry> _backpack = new List<BackpackEntry>();

        public IReadOnlyList<EquipEntry> Equipment => _equipment;
        public IReadOnlyList<BackpackEntry> Backpack => _backpack;
    }
}