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

        /// <summary>Server-only. Saca un item por índice (para el saqueo futuro). Devuelve el stack sacado.
        /// NO despawnea acá — quien llama debe llamar ServerDespawnIfEmpty() cuando termine toda la
        /// operación (por si necesita devolver algo al contenedor antes).</summary>
        [Server]
        public bool ServerTryTake(int index, out ItemStack taken)
        {
            taken = ItemStack.Empty;
            if (index < 0 || index >= _contents.Count) return false;

            taken = _contents[index];
            _contents.RemoveAt(index);
            return true;
        }


        /// <summary>Server-only. Agrega un stack al contenedor. Devuelve lo que no entró (siempre 0 por ahora, lista sin límite).</summary>
        [Server]
        public void ServerDeposit(ItemStack stack)
        {
            if (stack.IsEmpty) return;
            // Intentar merge con stacks existentes del mismo item apilable.
            for (int i = 0; i < _contents.Count; i++)
            {
                ItemStack s = _contents[i];
                if (s.ItemId != stack.ItemId) continue;
                // Necesitamos la def para saber si es apilable y el MaxStack.
                // LootContainer no tiene ItemDatabase, así que merge sin cap (cap = int.MaxValue).
                // El cap real lo valida RunInventory al sacar.
                _contents[i] = new ItemStack(s.ItemId, s.Quantity + stack.Quantity, s.Durability);
                return;
            }
            _contents.Add(stack);
        }


        /// <summary>Server-only. Reemplaza o elimina un item por índice (para swap con el inventario).
        /// NO despawnea acá — mismo motivo que ServerTryTake.</summary>
        [Server]
        public void ServerUpdateAt(int index, ItemStack stack)
        {
            if (index < 0 || index >= _contents.Count) return;
            if (stack.IsEmpty)
                _contents.RemoveAt(index);
            else
                _contents[index] = stack;
        }

        /// <summary>Server-only. Despawnea el contenedor si quedó vacío. Llamar al FINAL de una
        /// operación completa (después de cualquier depósito de vuelta), nunca en medio de una
        /// secuencia de mutaciones — si no, se pierde lo que faltaba depositar.</summary>
        [Server]
        public void ServerDespawnIfEmpty()
        {
            if (_contents.Count == 0)
                Despawn();
        }



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