using FishNet.Object;
using FishNet.Object.Synchronizing;
using Game.Core.Items;
using UnityEngine;

namespace Game.Presentation.Combat
{
    /// <summary>
    /// Un item tirado en el mundo (NetworkObject). Guarda qué item es y cuánto (ItemStack),
    /// sincronizado. Server-authoritative: el servidor decide cuándo se recoge y despawnea.
    /// </summary>
    public class WorldItem : NetworkBehaviour
    {
        // Estado sincronizado del item que representa.
        private readonly SyncVar<string> _itemId = new SyncVar<string>();
        private readonly SyncVar<int> _quantity = new SyncVar<int>();
        private readonly SyncVar<float> _durability = new SyncVar<float>();

        public string ItemId => _itemId.Value;
        public int Quantity => _quantity.Value;

        /// <summary>Server-only. Configura qué item representa este WorldItem.</summary>
        [Server]
        public void ServerSetItem(ItemStack stack)
        {
            _itemId.Value = stack.ItemId;
            _quantity.Value = stack.Quantity;
            _durability.Value = stack.Durability;
        }

        /// <summary>Server-only. Devuelve el stack que contiene.</summary>
        public ItemStack ToStack() => new ItemStack(_itemId.Value, _quantity.Value, _durability.Value);

        /// <summary>Nombre para el prompt (resuelto en cliente vía database).</summary>
        public string GetDisplayName(ItemDatabase db)
        {
            ItemSO def = db != null ? db.GetById(_itemId.Value) : null;
            return def != null ? def.DisplayName : _itemId.Value;
        }
    }
}