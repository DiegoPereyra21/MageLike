using System.Threading.Tasks;
using Game.Core.Items;
using Game.Core.Run;

namespace Game.Presentation.Run
{
    /// <summary>
    /// Implementación local (in-memory, se pierde al cerrar el juego) de IPlayerLoadoutStorage.
    /// Backend por defecto hasta que se conecte PlayFab. No hace red: resuelve el Task al toque.
    /// </summary>
    public class LocalPlayerLoadoutStorage : IPlayerLoadoutStorage
    {
        private InventorySnapshot _stored;

        public Task<InventorySnapshot> LoadAsync() => Task.FromResult(_stored);

        public Task SaveAsync(InventorySnapshot snapshot)
        {
            _stored = snapshot;
            return Task.CompletedTask;
        }
    }
}