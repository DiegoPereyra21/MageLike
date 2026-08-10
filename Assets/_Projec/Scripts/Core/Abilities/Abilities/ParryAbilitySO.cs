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

        [Header("Posición (relativa al jugador, sigue la mirada)")]
        [Tooltip("Altura del centro del parry respecto a los pies del jugador.")]
        [SerializeField] private float _heightOffset = 1f;
        [Tooltip("Distancia hacia adelante en la dirección de la mirada.")]
        [SerializeField] private float _forwardOffset = 1.5f;

        [Header("Tamaño (visual y detección, siempre iguales)")]
        [Tooltip("Radio de la esfera de detección Y escala del VFX.")]
        [SerializeField] private float _parryRadius = 1.5f;
        [Tooltip("Multiplicador visual por si el VFX base no mide exactamente 1 unidad de radio.")]
        [SerializeField] private float _vfxScalePerUnit = 1f;

        [Header("Recompensa")]
        [SerializeField] private float _manaRestore = 25f;

        public float StartupDuration  => _startupDuration;
        public float ActiveDuration   => _activeDuration;
        public float RecoveryDuration => _recoveryDuration;
        public float HeightOffset     => _heightOffset;
        public float ForwardOffset    => _forwardOffset;
        public float ParryRadius      => _parryRadius;
        public float VfxScale         => _parryRadius * _vfxScalePerUnit;
        public float ManaRestore      => _manaRestore;

        public override void Execute(AbilityExecutor executor, in AbilityCastContext context)
        {
            executor.StartParry(this, context);
        }
    }
}