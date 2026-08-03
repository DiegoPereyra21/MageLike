using UnityEngine;

namespace Game.Core.Abilities.Abilities
{
    [CreateAssetMenu(menuName = "Game/Abilities/Dash Ability", fileName = "Ability_Dash_")]
    public class DashAbilitySO : AbilitySO
    {
        [Header("Dash")]
        [SerializeField] private float _dashForce = 12f;

        public override void Execute(AbilityExecutor executor, in AbilityCastContext context)
        {
            Vector3 impulse = context.AimDirection.normalized * _dashForce;
            executor.ApplyMovementImpulse(context.CasterNetworkId, impulse);
        }
    }
}
