using System.Collections.Generic;
using Game.Core.Items;

namespace Game.Core.Run
{
    /// <summary>
    /// Almacén persistente del jugador (fuera de la run). Contrato: la implementación real
    /// (memoria ahora, UGS Cloud Save después) se enchufa sin tocar el resto.
    /// </summary>
    public interface IStashStorage
    {
        void Deposit(IEnumerable<ItemStack> items);
        IReadOnlyList<ItemStack> GetAll();
    }
}