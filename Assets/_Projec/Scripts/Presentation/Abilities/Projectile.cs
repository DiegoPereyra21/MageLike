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

            if (Physics.SphereCast(startPos, _radius, _direction, out RaycastHit hit, stepDistance, ~0, QueryTriggerInteraction.Ignore))
            {

                if (hit.collider.TryGetComponent(out NetworkObject nob) && nob.ObjectId == _casterNetworkId)
                {
                    transform.position = startPos + _direction * stepDistance;
                }
                else
                {

                    if (hit.collider.TryGetComponent(out IDamageable damageable))
                        damageable.ApplyDamage(_damage, _casterNetworkId);

                    transform.position = hit.point;

                    if (InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(_casterNetworkId, out NetworkObject casterNob))
                    {
                        if (casterNob.TryGetComponent(out AbilityController abilityController))
                        {
                            abilityController.NotifyProjectileImpact();
                        }
                    }

                    base.Despawn();
                    return;
                }
            }
            else
            {
                transform.position = startPos + _direction * stepDistance;
            }

            if (Time.time - _spawnTime >= _lifetime)
            {
                base.Despawn();
            }
        }
    }
}