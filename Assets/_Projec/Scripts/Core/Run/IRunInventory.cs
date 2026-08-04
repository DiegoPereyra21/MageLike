namespace Game.Core.Run
{
    /// <summary>
    /// Contrato del inventario de run. Todavía sin implementación concreta: existe para
    /// que extracción y muerte llamen al comportamiento correcto sin acoplarse a un
    /// inventario real. Cuando se implemente el inventario, solo hay que cumplir esta interfaz.
    /// </summary>
    public interface IRunInventory
    {
        /// <summary>Consolida el loot de la run al stash persistente (extracción exitosa).</summary>
        void CommitToStash();

        /// <summary>Suelta / pierde el loot de la run (muerte).</summary>
        void DropAll();
    }
}