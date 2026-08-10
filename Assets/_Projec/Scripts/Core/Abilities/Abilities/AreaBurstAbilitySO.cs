using UnityEngine;

namespace Game.Core.Abilities.Abilities
{
    [CreateAssetMenu(menuName = "Game/Abilities/Area Burst Ability", fileName = "Ability_AreaBurst_")]
    public class AreaBurstAbilitySO : AbilitySO
    {
        [Header("Orbe")]
        [SerializeField] private GameObject _orbPrefab;

        public override void Execute(AbilityExecutor executor, in AbilityCastContext context)
        {
            executor.SpawnOrb(_orbPrefab, context.Origin, context.AimDirection, context.CasterNetworkId, context.DamageMultiplier);
        }
    }
}