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
            // El punto de impacto ya viene resuelto (raycast hecho en el controller);
            // acá solo se aplica el efecto de área data-driven.
            executor.ApplyAreaEffect(context.AimPoint, _radius, _damage, context.CasterNetworkId);
        }
    }
}
