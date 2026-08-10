using FishNet;
using FishNet.Object;
using Game.Core.Abilities;
using Game.Presentation.Abilities;
using UnityEngine;

namespace Game.Presentation.Combat
{
    /// <summary>
    /// Ballistic projectile: follows a parabolic arc toward a target point,
    /// explodes on impact (small area damage). Server-authoritative.
    /// </summary>
    public class LobProjectile : NetworkBehaviour
    {
        [Header("Explosion")]
        [SerializeField] private float _explosionRadius = 2f;
        [SerializeField] private float _lifetime = 8f;

        private Vector3 _velocity;
        private float _gravity;
        private float _damage;
        private int _casterNetworkId;
        private float _spawnTime;
        private bool _initialized;
        private bool _exploded;

        private bool _firstFrame;

        [Server]
        public void Initialize(Vector3 targetPoint, float arcHeight, float flightTime, float damage, int casterNetworkId)
        {
            _damage = damage;
            _casterNetworkId = casterNetworkId;
            _spawnTime = Time.time;

            Vector3 toTarget = targetPoint - transform.position;
            Vector3 flatDelta = new Vector3(toTarget.x, 0f, toTarget.z);

            _gravity = -8f * arcHeight / (flightTime * flightTime);
            float verticalVelocity = (toTarget.y - 0.5f * _gravity * flightTime * flightTime) / flightTime;

            _velocity = flatDelta / flightTime + Vector3.up * verticalVelocity;

            // Orient nose toward initial velocity right away so it doesn't render facing identity.
            if (_velocity.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(_velocity.normalized);

            _initialized = true;
            _firstFrame = true;
        }

        private void Update()
        {
            if (!base.IsServerStarted) return;
            if (!_initialized || _exploded) return;

            // Skip the spawn frame: its deltaTime is unreliable and would produce a bad first step.
            if (_firstFrame)
            {
                _firstFrame = false;
                return;
            }

            _velocity.y += _gravity * Time.deltaTime;
            Vector3 step = _velocity * Time.deltaTime;

            // Collision check along this frame's movement.
            if (Physics.SphereCast(transform.position, 0.3f, step.normalized, out RaycastHit hit,
                    step.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                // Ignore the caster (the plant itself).
                if (!(hit.collider.TryGetComponent(out NetworkObject nob) && nob.ObjectId == _casterNetworkId))
                {
                    Explode(hit.point);
                    return;
                }
            }

            transform.position += step;

            // Face the movement direction (nose-first arc).
            if (step.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(step.normalized);

            if (Time.time - _spawnTime >= _lifetime)
                Explode(transform.position);
        }

        [Server]
        private void Explode(Vector3 point)
        {
            _exploded = true;

            Collider[] hits = Physics.OverlapSphere(point, _explosionRadius);
            foreach (Collider hit in hits)
            {
                if (hit.TryGetComponent(out NetworkObject nob) && nob.ObjectId == _casterNetworkId)
                    continue;
                if (hit.TryGetComponent(out IDamageable damageable))
                    damageable.ApplyDamage(_damage, _casterNetworkId);
            }

            ShowExplosionObserversRpc(point);
            base.Despawn();
        }

        [ObserversRpc]
        private void ShowExplosionObserversRpc(Vector3 point)
        {
            VFXManager.PlayOrbExplosion(point);
        }
    }
}