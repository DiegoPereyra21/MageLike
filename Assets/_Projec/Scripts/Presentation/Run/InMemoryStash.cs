using System.Collections.Generic;
using Game.Core.Items;
using Game.Core.Run;

namespace Game.Presentation.Run
{
    /// <summary>
    /// Stash en memoria (se pierde al cerrar). Implementación temporal de IStashStorage;
    /// se reemplaza por UGS Cloud Save / backend sin tocar quien lo usa.
    /// </summary>
    public class InMemoryStash : IStashStorage
    {
        private readonly List<ItemStack> _items = new List<ItemStack>();

        public void Deposit(IEnumerable<ItemStack> items)
        {
            foreach (var it in items)
                if (!it.IsEmpty) _items.Add(it);
        }

        public IReadOnlyList<ItemStack> GetAll() => _items;
    }
}