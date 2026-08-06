using UnityEngine;

namespace Game.Core.Abilities.Abilities
{
    [CreateAssetMenu(menuName = "Game/Abilities/Area Burst Ability", fileName = "Ability_AreaBurst_")]
    public class AreaBurstAbilitySO : AbilitySO
    {
        [Header("Área")]
        [SerializeField] private float _radius = 4f;
        [SerializeField] private float _damage = 35f;
        [SerializeField] private float _maxCastDistance = 15f;

        public override void Execute(AbilityExecutor executor, in AbilityCastContext context)
        {
            float finalDamage = _damage * context.DamageMultiplier;
            executor.ApplyAreaEffect(context.AimPoint, _radius, finalDamage, context.CasterNetworkId);
        }
    }
}
