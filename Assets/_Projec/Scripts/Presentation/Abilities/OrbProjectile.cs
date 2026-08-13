using FishNet;
using FishNet.Object;
using Game.Core.Abilities;
using Game.Presentation.Combat;
using UnityEngine;

namespace Game.Presentation.Abilities
{
    /// <summary>
    /// Orbe explosivo de área. Vuela lento, muestra radio de explosión creciendo,
    /// explota al impactar o al llegar a distancia máxima.
    /// Server-authoritative; VFX de explosión via ObserversRpc.
    /// </summary>
    public class OrbProjectile : NetworkBehaviour
    {
        [Header("Movimiento")]
        [SerializeField] private float _speed = 8f;
        [SerializeField] private float _maxDistance = 20f;

        [Header("Explosión")]
        [SerializeField] private float _explosionRadius = 4f;
        [SerializeField] private float _damage = 35f;

        [Header("Visual")]
        [SerializeField] private Transform _radiusIndicator; // esfera hija que crece

        private Vector3 _direction;
        private float _travelledDistance;
        private int _casterNetworkId;
        private float _damageMultiplier = 1f;
        private int _slot = -1; // slot casteado; para resolver el clip de audio localmente en cada cliente
        private bool _exploded;

        [Server]
        public void Initialize(Vector3 direction, int casterNetworkId, float damageMultiplier, int slot = -1)
        {
            _direction = direction.normalized;
            _casterNetworkId = casterNetworkId;
            _damageMultiplier = damageMultiplier;
            _slot = slot;
            _travelledDistance = 0f;
            _exploded = false;
        }

        private void Update()
        {
            if (!base.IsServerStarted) return;
            if (_exploded) return;

            float step = _speed * Time.deltaTime;
            Vector3 startPos = transform.position;

            // Crecer el indicador de radio visualmente (sincronizado por NetworkTransform del hijo).
            float progress = _travelledDistance / _maxDistance;
            float currentRadius = Mathf.Lerp(0.1f, _explosionRadius, progress);
            if (_radiusIndicator != null)
                _radiusIndicator.localScale = Vector3.one * currentRadius * 2f;

            // Detectar impacto.
            if (Physics.SphereCast(startPos, 0.3f, _direction, out RaycastHit hit, step, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.TryGetComponent(out NetworkObject nob) && nob.ObjectId == _casterNetworkId)
                {
                    // Ignorar al caster, seguir de largo.
                    transform.position = startPos + _direction * step;
                    _travelledDistance += step;
                }
                else
                {
                    Explode(hit.point);
                    return;
                }
            }
            else
            {
                transform.position = startPos + _direction * step;
                _travelledDistance += step;
            }

            // Distancia máxima alcanzada.
            if (_travelledDistance >= _maxDistance)
                Explode(transform.position);
        }

        [Server]
        private void Explode(Vector3 point)
        {
            _exploded = true;

            // Daño a todos en el radio.
            float finalDamage = _damage * _damageMultiplier;
            bool hitConfirmed = false;
            bool isKill = false;

            Collider[] hits = Physics.OverlapSphere(point, _explosionRadius);
            foreach (Collider hit in hits)
            {
                if (hit.TryGetComponent(out NetworkObject nob) && nob.ObjectId == _casterNetworkId)
                    continue; // no dañar al caster
                if (hit.TryGetComponent(out IDamageable damageable))
                {
                    damageable.ApplyDamage(finalDamage, _casterNetworkId);
                    hitConfirmed = true;
                    if (damageable is Health health && health.IsDead)
                        isKill = true;
                }
            }

            // VFX en todos los clientes.
            ShowExplosionObserversRpc(point, _explosionRadius);

            // Screen shake + hitmarker + audio de impacto al caster.
            if (InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(_casterNetworkId, out NetworkObject casterNob) &&
                casterNob.TryGetComponent(out AbilityController ac))
            {
                ac.NotifyProjectileImpact(point, Vector3.up, hitConfirmed, isKill);
                ac.NotifyAbilityImpactSfx(point, _slot); // la explosión suena siempre, haya dañado a alguien o no
            }

            base.Despawn();
        }

        [ObserversRpc]
        private void ShowExplosionObserversRpc(Vector3 point, float radius)
        {
        VFXManager.PlayOrbExplosion(point);        
        }
    }
}