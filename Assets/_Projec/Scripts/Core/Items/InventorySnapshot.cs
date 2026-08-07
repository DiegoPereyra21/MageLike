using System.Collections.Generic;

namespace Game.Core.Items
{
    /// <summary>
    /// "Foto" del inventario propio del jugador: equipamiento por slot + items de mochila.
    /// Es lo que persiste entre runs y se restaura al entrar. Listas planas de ItemStack,
    /// listas para serializar (UGS Cloud Save a futuro).
    /// </summary>
    [System.Serializable]
    public class InventorySnapshot
    {
        public List<ItemStack> Equipment = new List<ItemStack>();
        public List<ItemStack> Backpack = new List<ItemStack>();

        public bool IsEmpty => Equipment.Count == 0 && Backpack.Count == 0;
    }
}