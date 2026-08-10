using UnityEngine;

namespace Game.Core.Abilities.Abilities
{
    [CreateAssetMenu(menuName = "Game/Abilities/Parry Ability", fileName = "Ability_Parry_")]
    public class ParryAbilitySO : AbilitySO
    {
        [Header("Timing")]
        [SerializeField] private float _startupDuration = 0.1f;
        [SerializeField] private float _activeDuration = 0.2f;
        [SerializeField] private float _recoveryDuration = 0.15f;

        [Header("Detección")]
        [SerializeField] private float _parryRadius = 2.5f;

        [Header("Recompensa")]
        [SerializeField] private float _manaRestore = 25f;

        public float StartupDuration  => _startupDuration;
        public float ActiveDuration   => _activeDuration;
        public float RecoveryDuration => _recoveryDuration;
        public float ParryRadius      => _parryRadius;
        public float ManaRestore      => _manaRestore;

        public override void Execute(AbilityExecutor executor, in AbilityCastContext context)
        {
            executor.StartParry(this, context);
        }
    }
}