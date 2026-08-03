using FishNet;
using FishNet.Object;
using Game.Core.Abilities;
using Game.Presentation.Abilities;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Presentation.Bootstrap
{
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<AbilityExecutor, NetworkAbilityExecutor>(Lifetime.Singleton);
        }

        protected override void Awake()
        {
            base.Awake();

            // Cuando un NetworkObject se instancia por red (incluye jugadores y proyectiles
            // no pooled aún), resolvemos manualmente sus componentes inyectables.
            InstanceFinder.NetworkManager.SceneManager.OnClientLoadedStartScenes += (_, _) => { };
        }

        // Llamar desde el propio NetworkBehaviour (ej. AbilityController.OnStartNetwork)
        // pasando el objeto recién spawneado, ya que VContainer no lo inyecta automáticamente.
        public void InjectSpawnedObject(NetworkObject spawned)
        {
            Container.InjectGameObject(spawned.gameObject);
        }
    }
}