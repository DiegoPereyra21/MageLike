using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.Items
{
    /// <summary>
    /// Tabla de loot data-driven. Cada entrada tiene un item, un rango de cantidad y una
    /// probabilidad de aparecer. Al resolver, cada entrada se tira por separado (rolls
    /// independientes), así un enemigo puede soltar varias cosas o ninguna.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Items/Loot Table", fileName = "LootTable_")]
    public class LootTableSO : ScriptableObject
    {
        [System.Serializable]
        public struct LootEntry
        {
            public ItemSO Item;
            [Range(0f, 1f)] public float Chance;   // 0..1 probabilidad de que aparezca
            [Min(1)] public int MinQuantity;
            [Min(1)] public int MaxQuantity;
        }

        [SerializeField] private List<LootEntry> _entries = new List<LootEntry>();

        /// <summary>Resuelve la tabla: devuelve los ItemStack que salieron (puede ser lista vacía).</summary>
        public List<ItemStack> Roll()
        {
            var result = new List<ItemStack>();
            foreach (var entry in _entries)
            {
                if (entry.Item == null) continue;
                if (Random.value > entry.Chance) continue; // no salió

                int qty = Random.Range(entry.MinQuantity, entry.MaxQuantity + 1);
                if (qty <= 0) continue;

                result.Add(new ItemStack(entry.Item.ItemId, qty, 1f));
            }
            return result;
        }
    }
}