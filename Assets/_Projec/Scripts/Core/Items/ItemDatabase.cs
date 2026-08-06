using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.Items
{
    /// <summary>
    /// Registro central de todos los items del juego. Lista manual: arrastrar cada ItemSO
    /// acá. Mapea itemId (lo que viaja por la red) a su definición (ItemSO).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Items/Item Database", fileName = "ItemDatabase")]
    public class ItemDatabase : ScriptableObject
    {
        [SerializeField] private List<ItemSO> _items = new List<ItemSO>();

        private Dictionary<string, ItemSO> _lookup;

        private void BuildLookup()
        {
            _lookup = new Dictionary<string, ItemSO>();
            foreach (var item in _items)
            {
                if (item == null || string.IsNullOrEmpty(item.ItemId)) continue;
                if (!_lookup.ContainsKey(item.ItemId))
                    _lookup[item.ItemId] = item;
                else
                    Debug.LogWarning($"[ItemDatabase] ItemId duplicado: {item.ItemId}");
            }
        }

        public ItemSO GetById(string itemId)
        {
            if (_lookup == null) BuildLookup();
            if (string.IsNullOrEmpty(itemId)) return null;
            return _lookup.TryGetValue(itemId, out var item) ? item : null;
        }
    }
}