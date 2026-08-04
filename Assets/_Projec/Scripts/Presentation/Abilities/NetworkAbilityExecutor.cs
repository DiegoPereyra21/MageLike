using FishNet;
using FishNet.Object;
using Game.Core.Abilities;
using UnityEngine;

namespace Game.Presentation.Abilities
{
    /// <summary>
    /// Implementación real de AbilityExecutor. Solo debe ejecutarse server-side
    /// (las AbilitySO.Execute se llaman desde CastServerRpc, que ya corre en el servidor).
    /// Usa el pooling NATIVO de Fish-Net para prefabs con NetworkObject: para que reutilice
    /// instancias en vez de Instantiate/Destroy, activar "Enable Pooling" en el NetworkObject
    /// del prefab del proyectil (Inspector) — no hace falta pooling manual acá.
    /// </summary>
    public class NetworkAbilityExecutor : AbilityExecutor
    {
        public void SpawnProjectile(GameObject projectilePrefab, Vector3 origin, Vector3 direction, float speed, float damage, float radius, int casterNetworkId)
        {
            if (!InstanceFinder.IsServerStarted) return;

            GameObject instance = InstanceFinder.NetworkManager.GetPooledInstantiated(
                projectilePrefab.GetComponent<NetworkObject>(), asServer: true).gameObject;

            Vector3 spawnPos = origin + direction.normalized * 1.2f;
            instance.transform.SetPositionAndRotation(spawnPos, Quaternion.LookRotation(direction));

            if (instance.TryGetComponent(out Projectile projectile))
                projectile.Initialize(direction, speed, damage, radius, casterNetworkId);

            InstanceFinder.ServerManager.Spawn(instance);
        }

        public void ApplyAreaEffect(Vector3 point, float radius, float damage, int casterNetworkId)
        {
            if (!InstanceFinder.IsServerStarted) return;

            Collider[] hits = Physics.OverlapSphere(point, radius);
            foreach (Collider hit in hits)
            {
                if (hit.TryGetComponent(out IDamageable damageable))
                    damageable.ApplyDamage(damage, casterNetworkId);
            }
        }

        public void ApplyMovementImpulse(int casterNetworkId, Vector3 impulse)
        {
            if (!InstanceFinder.IsServerStarted) return;
            if (!InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(casterNetworkId, out NetworkObject nob)) return;

            // Simplificación del vertical slice: el dash se aplica como salto de posición
            // validado server-side. Para una versión pulida, este impulso debería inyectarse
            // dentro del ReplicateData de PlayerMovementController para que quede
            // correctamente predicho/reconciliado en vez de ser un salto discreto.
            nob.transform.position += impulse * Time.fixedDeltaTime;
        }

        public void ApplySelfEffect(int casterNetworkId, float healAmount)
        {
            if (!InstanceFinder.IsServerStarted) return;
            if (!InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(casterNetworkId, out NetworkObject nob)) return;

            if (nob.TryGetComponent(out IDamageable damageable))
                damageable.ApplyDamage(-healAmount, casterNetworkId);
        }
    }
}
