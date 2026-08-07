using Game.Core.Items;

namespace Game.Presentation.Run
{
    /// <summary>
    /// Acceso global al stash (con slots). Persistente en memoria entre runs; listo para
    /// UGS Cloud Save después.
    /// </summary>
    public static class StashService
    {
        private static StashData _stash;

        public static StashData Stash
        {
            get
            {
                _stash ??= new StashData();
                return _stash;
            }
        }
    }
}