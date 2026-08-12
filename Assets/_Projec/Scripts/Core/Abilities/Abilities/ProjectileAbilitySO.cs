using UnityEngine;

namespace Game.Core.Abilities.Abilities
{
    [CreateAssetMenu(menuName = "Game/Abilities/Projectile Ability", fileName = "Ability_Projectile_")]
    public class ProjectileAbilitySO : AbilitySO
    {
        [Header("Proyectil")]
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private float _speed = 25f;
        [SerializeField] private float _damage = 20f;
        [SerializeField] private float _radius = 0.25f;
        [Tooltip("Prefab visual-only (sin NetworkObject) que ve el tirador al instante. Ver CosmeticProjectile.")]
        [SerializeField] private GameObject _cosmeticProjectilePrefab;

        public override void Execute(AbilityExecutor executor, in AbilityCastContext context)
        {
            float finalDamage = _damage * context.DamageMultiplier;

            // Sale del SpellOrigin autoritativo (context.Origin) y converge hacia el punto de mira.
            Vector3 toAim = context.AimPoint - context.Origin;
            Vector3 direction = toAim.sqrMagnitude > 0.0001f ? toAim.normalized : context.AimDirection;

            executor.SpawnProjectile(
                _projectilePrefab,
                context.Origin,
                direction,
                _speed,
                finalDamage,
                _radius,
                context.CasterNetworkId,
                context.Tick        // tick de disparo del cliente (lag comp)
            );
        }

        public override bool TryGetCosmeticProjectile(out GameObject prefab, out float speed)
        {
            prefab = _cosmeticProjectilePrefab;
            speed = _speed;
            return prefab != null;
        }
    }
}