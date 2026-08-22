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

        [Tooltip("Si ninguna entrada sale en el roll normal, fuerza una al azar (ponderada por Chance) para que la tabla nunca devuelva vacío. Pensado para cofres; los enemigos lo dejan en false para poder no soltar nada.")]
        [SerializeField] private bool _guaranteeAtLeastOne;

        /// <summary>Resuelve la tabla: devuelve los ItemStack que salieron (puede ser lista vacía si _guaranteeAtLeastOne es false).</summary>
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

            if (_guaranteeAtLeastOne && result.Count == 0)
            {
                var forced = PickWeightedEntry();
                if (forced.Item != null)
                {
                    int qty = Random.Range(forced.MinQuantity, forced.MaxQuantity + 1);
                    if (qty > 0) result.Add(new ItemStack(forced.Item.ItemId, qty, 1f));
                }
            }

            return result;
        }

        private LootEntry PickWeightedEntry()
        {
            float total = 0f;
            foreach (var e in _entries)
                if (e.Item != null) total += Mathf.Max(e.Chance, 0.0001f);

            if (total <= 0f) return default;

            float roll = Random.Range(0f, total);
            float cursor = 0f;
            foreach (var e in _entries)
            {
                if (e.Item == null) continue;
                cursor += Mathf.Max(e.Chance, 0.0001f);
                if (roll <= cursor) return e;
            }
            return default;
        }
    }
}