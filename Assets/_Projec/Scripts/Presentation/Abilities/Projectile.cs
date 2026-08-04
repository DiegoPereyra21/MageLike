using FishNet.Object;
using Game.Core.Abilities;
using UnityEngine;

namespace Game.Presentation.Abilities
{
    /// <summary>
    /// Proyectil server-authoritative con detección por tramo (SphereCast entre la posición
    /// del tick anterior y la nueva). Evita tunneling a cualquier velocidad y respeta el
    /// grosor del proyectil. El caster se descarta por ObjectId.
    /// Prefab con NetworkObject (Default Despawn Type = Pool). El collider es solo visual/opcional.
    /// </summary>
    public class Projectile : NetworkBehaviour
    {
        [SerializeField] private float _lifetime = 5f;

        private Vector3 _direction;
        private float _speed;
        private float _damage;
        private float _radius;
        private int _casterNetworkId;
        private float _spawnTime;

        public void Initialize(Vector3 direction, float speed, float damage, float radius, int casterNetworkId)
        {
            _direction = direction.normalized;
            _speed = speed;
            _damage = damage;
            _radius = radius;
            _casterNetworkId = casterNetworkId;
            _spawnTime = Time.time;
        }

        private void Update()
        {
            if (!base.IsServerStarted) return;

            float stepDistance = _speed * Time.deltaTime;
            Vector3 startPos = transform.position;

            // SphereCast por el tramo que recorrería este frame.
            if (Physics.SphereCast(startPos, _radius, _direction, out RaycastHit hit, stepDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                // Ignorar al propio caster.
                if (hit.collider.TryGetComponent(out NetworkObject nob) && nob.ObjectId == _casterNetworkId)
                {
                    // Sigue de largo: avanzá igual y no impactes al caster.
                    transform.position = startPos + _direction * stepDistance;
                }
                else
                {
                    if (hit.collider.TryGetComponent(out IDamageable damageable))
                        damageable.ApplyDamage(_damage, _casterNetworkId);

                    transform.position = hit.point;
                    base.Despawn();
                    return;
                }
            }
            else
            {
                transform.position = startPos + _direction * stepDistance;
            }

            if (Time.time - _spawnTime >= _lifetime)
                base.Despawn();
        }
    }
}