using System.Threading.Tasks;
using Game.Core.Items;
using Game.Core.Run;

namespace Game.Presentation.Run
{
    /// <summary>
    /// Implementación local (in-memory, se pierde al cerrar el juego) de IStashStorage.
    /// Backend por defecto hasta que se conecte PlayFab. No hace red: resuelve el Task al toque.
    /// </summary>
    public class LocalStashStorage : IStashStorage
    {
        private StashData _stored;

        public Task<StashData> LoadAsync() => Task.FromResult(_stored);

        public Task SaveAsync(StashData stash)
        {
            _stored = stash;
            return Task.CompletedTask;
        }
    }
}