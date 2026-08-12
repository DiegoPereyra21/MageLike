using UnityEngine;

namespace Game.Core.Abilities
{
    /// <summary>
    /// Contrato común para toda habilidad. Se implementa vía ScriptableObject para
    /// permitir diseño data-driven (nuevas habilidades sin tocar código de sistema).
    /// </summary>
    public interface IAbility
    {
        string AbilityId { get; }
        float Cooldown { get; }
        float ResourceCost { get; }

        /// <summary>
        /// Ejecuta el efecto de la habilidad. Se llama SIEMPRE desde el servidor
        /// (autoridad) para el efecto real; el cliente solo predice feedback local (VFX/anim).
        /// </summary>
        void Execute(AbilityExecutor executor, in AbilityCastContext context);
    }

    /// <summary>
    /// Clase base abstracta para todas las habilidades como asset (ScriptableObject).
    /// Cada habilidad concreta hereda de acá e implementa su propio comportamiento:
    /// composición sobre herencia para EFECTOS (buffs/debuffs), pero herencia simple
    /// aquí porque todas comparten el mismo contrato de ejecución.
    /// </summary>
    public abstract class AbilitySO : ScriptableObject, IAbility
    {
        [Header("Identidad")]
        [SerializeField] private string _abilityId;
        [SerializeField] private string _displayName;
        [SerializeField] private Sprite _icon;

        [Header("Costos")]
        [SerializeField] private float _cooldown = 1f;
        [SerializeField] private float _resourceCost = 10f;

        [Header("VFX")]
        [SerializeField] private GameObject _muzzlePrefab;
        public GameObject MuzzlePrefab => _muzzlePrefab;
        
        public string AbilityId => _abilityId;
        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public float Cooldown => _cooldown;
        public float ResourceCost => _resourceCost;

        /// <summary>
        /// Datos del proyectil cosmético local (visual-only) que el cliente del tirador spawnea
        /// para feedback instantáneo. Por defecto ninguna: solo las habilidades de proyectil lo dan.
        /// </summary>
        public virtual bool TryGetCosmeticProjectile(out GameObject prefab, out float speed)
        {
            prefab = null;
            speed = 0f;
            return false;
        }

        /// <summary>
        /// Datos de dash para que el OWNER lo prediga localmente al castear (Prediction v2:
        /// cliente predice, server aplica autoritativo, reconcile alinea). Por defecto no es dash.
        /// </summary>
        public virtual bool TryGetOwnerDash(Vector3 aimDirection, out Vector3 direction, out float speed, out float duration)
        {
            direction = default; speed = 0f; duration = 0f;
            return false;
        }

        public abstract void Execute(AbilityExecutor executor, in AbilityCastContext context);
    }
}
