using FishNet;
using FishNet.Object;
using Game.Core.Abilities;
using Game.Presentation.Combat;
using UnityEngine;

namespace Game.Presentation.Abilities
{
    public class Projectile : NetworkBehaviour
    {
        [SerializeField] private float _lifetime = 5f;

        [Tooltip("Contra qué castea el proyectil. Debe incluir Hitbox (objetivos) y Ground (paredes/piso). " +
                 "Si queda vacío, se resuelve por nombre en Awake.")]
        [SerializeField] private LayerMask _hitMask;

        private Vector3 _direction;
        private float _speed;
        private float _damage;
        private float _radius;
        private int _casterNetworkId;
        private float _aliveTime;

        private bool _initialized;

        // Buffer compartido para el OverlapSphere sin allocations. Los proyectiles corren
        // en el tick del server (single-thread), así que reutilizarlo secuencialmente es seguro.
        private static readonly Collider[] _overlapBuffer = new Collider[16];

        private void Awake()
        {
            // Red de seguridad: si el prefab existente todavía no tiene la máscara seteada,
            // la resolvemos por nombre (independiente del índice de layer) para no castear contra Nothing.
            if (_hitMask.value == 0)
                _hitMask = LayerMask.GetMask("Hitbox", "Ground");
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            base.TimeManager.OnTick += OnTick;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (base.TimeManager != null)
                base.TimeManager.OnTick -= OnTick;
        }

        public void Initialize(Vector3 direction, float speed, float damage, float radius, int casterNetworkId)
        {
            _direction   = direction.normalized;
            _speed       = speed;
            _damage      = damage;
            _radius      = radius;
            _casterNetworkId = casterNetworkId;
            _aliveTime   = 0f;
            _initialized = true;
        }

        // Simulación a paso fijo (tick de red). Requisito del lag compensation: el hit se
        // resuelve en un tick concreto, que es al que el servidor puede rebobinar los colliders.
        private void OnTick()
        {
            if (!_initialized) return;

            float delta = (float)base.TimeManager.TickDelta;
            float stepDistance = _speed * delta;
            Vector3 startPos = transform.position;

            // 1) Caso "el enemigo se mete encima del proyectil": SphereCast NO detecta
            //    colliders que ya solapan la esfera en el origen. Un OverlapSphere previo
            //    cubre ese hueco (típico con enemigos que corren de frente a alta velocidad).
            int count = Physics.OverlapSphereNonAlloc(startPos, _radius, _overlapBuffer, _hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider col = _overlapBuffer[i];

                // Ignorar el propio collider del proyectil (y cualquier hijo suyo).
                if (col.transform.IsChildOf(transform)) continue;

                // El collider golpeable es un hijo (capa Hitbox); Health/NetworkObject están en el root.
                NetworkObject nob = col.GetComponentInParent<NetworkObject>();

                // Ignorar al caster.
                if (nob != null && nob.ObjectId == _casterNetworkId) continue;

                IDamageable dmg = nob != null ? nob.GetComponent<IDamageable>() : null; // null = geometría (Ground)
                ResolveImpact(startPos, -_direction, dmg);
                return;
            }

            // 2) Sweep normal a lo largo del paso de este tick.
            if (Physics.SphereCast(startPos, _radius, _direction, out RaycastHit hit, stepDistance, _hitMask, QueryTriggerInteraction.Ignore))
            {
                NetworkObject hitNob = hit.collider.GetComponentInParent<NetworkObject>();

                if (hitNob != null && hitNob.ObjectId == _casterNetworkId)
                {
                    // Atravesar al caster.
                    transform.position = startPos + _direction * stepDistance;
                }
                else
                {
                    IDamageable damageable = hitNob != null ? hitNob.GetComponent<IDamageable>() : null; // null = pared
                    ResolveImpact(hit.point, hit.normal, damageable);
                    return;
                }
            }
            else
            {
                transform.position = startPos + _direction * stepDistance;
            }

            _aliveTime += delta;
            if (_aliveTime >= _lifetime)
                base.Despawn();
        }

        /// <summary>
        /// Aplica el daño (si golpeó algo con vida), reposiciona en el punto de impacto,
        /// notifica al caster (screenshake + VFX vía AbilityController persistente, porque
        /// este NetworkObject se despawnea acá mismo) y despawnea.
        /// </summary>
        private void ResolveImpact(Vector3 point, Vector3 normal, IDamageable damageable)
        {
            if (damageable != null)
                damageable.ApplyDamage(_damage, _casterNetworkId);

            transform.position = point;

            if (InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(_casterNetworkId, out NetworkObject casterNob))
                if (casterNob.TryGetComponent(out AbilityController ac))
                    ac.NotifyProjectileImpact(point, normal);

            base.Despawn();
        }
    }
}