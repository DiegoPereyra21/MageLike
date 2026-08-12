using UnityEngine;

namespace Game.Core.Abilities.Abilities
{
    [CreateAssetMenu(menuName = "Game/Abilities/Dash Ability", fileName = "Ability_Dash_")]
    public class DashAbilitySO : AbilitySO
    {
        [Header("Dash")]
        [Tooltip("Velocidad inicial del dash (unidades/seg).")]
        [SerializeField] private float _dashSpeed = 22f;
        [Tooltip("Duración del dash en segundos.")]
        [SerializeField] private float _dashDuration = 0.2f;

        public float DashSpeed => _dashSpeed;
        public float DashDuration => _dashDuration;

        public override void Execute(AbilityExecutor executor, in AbilityCastContext context)
        {
            executor.StartDash(context.CasterNetworkId, context.AimDirection.normalized, _dashSpeed, _dashDuration);
        }

        public override bool TryGetOwnerDash(Vector3 aimDirection, out Vector3 direction, out float speed, out float duration)
        {
            direction = aimDirection.normalized;
            speed = _dashSpeed;
            duration = _dashDuration;
            return true;
        }
    }
}