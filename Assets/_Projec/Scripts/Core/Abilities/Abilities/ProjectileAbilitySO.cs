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

        public override void Execute(AbilityExecutor executor, in AbilityCastContext context)
        {
            executor.SpawnProjectile(
                _projectilePrefab,
                context.Origin,
                context.AimDirection,
                _speed,
                _damage,
                context.CasterNetworkId
            );
        }
    }
}
