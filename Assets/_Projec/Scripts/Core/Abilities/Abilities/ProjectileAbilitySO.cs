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
    }
}