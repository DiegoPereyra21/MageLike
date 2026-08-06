using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Game.Core.Items;
using UnityEngine;

namespace Game.Presentation.Combat
{
    /// <summary>
    /// Contenedor de loot en el mundo (cadáver, y a futuro mochila tirada). Guarda varios
    /// items (SyncList), server-authoritative. El saqueo (ver/sacar) se hará vía UI en la capa 5.
    /// </summary>
    public class LootContainer : NetworkBehaviour
    {
        private readonly SyncList<ItemStack> _contents = new SyncList<ItemStack>();

        public IReadOnlyList<ItemStack> Contents => _contents;
        public event System.Action OnContentsChanged;

        /// <summary>Server-only. Llena el contenedor con una colección de items.</summary>
        [Server]
        public void ServerFill(IEnumerable<ItemStack> items)
        {
            foreach (var s in items)
                if (!s.IsEmpty) _contents.Add(s);
        }

        /// <summary>Server-only. Saca un item por índice (para el saqueo futuro). Devuelve el stack sacado.</summary>
        [Server]
        public bool ServerTryTake(int index, out ItemStack taken)
        {
            taken = ItemStack.Empty;
            if (index < 0 || index >= _contents.Count) return false;

            taken = _contents[index];
            _contents.RemoveAt(index);

            // Si queda vacío, despawnear el contenedor.
            if (_contents.Count == 0)
                Despawn();

            return true;
        }

        //inv
        public override void OnStartClient()
          {
          base.OnStartClient();
          _contents.OnChange += (op, index, oldItem, newItem, asServer) => OnContentsChanged?.Invoke();
          }
          public void RegisterChangeCallback(System.Action cb) => OnContentsChanged += cb;
          public void UnregisterChangeCallback(System.Action cb) => OnContentsChanged -= cb;

        public bool IsEmpty => _contents.Count == 0;
    }
}