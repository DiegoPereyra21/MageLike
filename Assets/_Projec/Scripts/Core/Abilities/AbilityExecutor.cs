using FishNet;
using FishNet.Object;
using Game.Core.Abilities.Abilities;
using UnityEngine;

namespace Game.Core.Abilities
{
    /// <summary>
    /// Puerto (en el sentido de arquitectura hexagonal) que las AbilitySO usan para producir
    /// efectos concretos. La implementación real vive en Presentation y sabe de FishNet/pooling;
    /// las habilidades en Core nunca dependen de eso directamente. Se inyecta con VContainer.
    /// </summary>
    public interface AbilityExecutor
    {
        /// <summary>Spawnea un proyectil de red desde el pool (server-authoritative). slot: para resolver audio localmente en cada cliente.</summary>
        void SpawnProjectile(GameObject projectilePrefab, Vector3 origin, Vector3 direction, float speed, float damage, float radius, int casterNetworkId, uint fireTick, int slot);
        /// <summary>Aplica daño/efecto en área alrededor de un punto (server-authoritative).</summary>
        void ApplyAreaEffect(Vector3 point, float radius, float damage, int casterNetworkId);

        /// <summary>Aplica un impulso de movimiento al caster (ej. dash), respetando predicción.</summary>
        public void StartDash(int casterNetworkId, Vector3 direction, float speed, float duration)
        {
            if (!InstanceFinder.IsServerStarted) return;
            if (!InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(casterNetworkId, out NetworkObject nob)) return;
            if (nob.TryGetComponent(out Game.Presentation.Player.PlayerMovementController movement))
                movement.StartDash(direction, speed, duration);
        }

        /// <summary>Aplica curación/buff directo al caster.</summary>
        void ApplySelfEffect(int casterNetworkId, float healAmount);

        public abstract void SpawnOrb(GameObject orbPrefab, Vector3 origin, Vector3 direction, int casterNetworkId, float damageMultiplier, int slot);
        void StartParry(Game.Core.Abilities.Abilities.ParryAbilitySO data, in AbilityCastContext context);
        
    }
}