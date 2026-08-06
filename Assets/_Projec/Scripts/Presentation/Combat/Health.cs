using FishNet.Object;
using FishNet.Object.Synchronizing;
using Game.Core.Abilities;
using UnityEngine;

namespace Game.Presentation.Combat
{
    /// <summary>
    /// Salud server-authoritative. El daño/cura solo se aplica en el servidor; el valor
    /// actual se sincroniza a los clientes vía SyncVar. Reusable para jugadores y enemigos.
    /// </summary>
    public class Health : NetworkBehaviour, IDamageable
    {
        [SerializeField] private PlayerStats _stats;
        [SerializeField] private float _maxHealth = 100f;

        private readonly SyncVar<float> _current = new SyncVar<float>();

        public float Current => _current.Value;
        public float Max => _maxHealth;
        public bool IsDead => _current.Value <= 0f;

        public override void OnStartServer()
        {
            _current.Value = _maxHealth;
        }

        /// <summary>
        /// amount positivo = daño, negativo = cura. Solo corre en servidor.
        /// </summary>
        public void ApplyDamage(float amount, int instigatorNetworkId)
        {
            if (!base.IsServerStarted) return;
            if (IsDead) return;

            // Protección: reduce solo el daño entrante (no afecta curas).
            if (amount > 0f && _stats != null)
                amount *= (1f - _stats.ProtectionPercent);

            float newValue = Mathf.Clamp(_current.Value - amount, 0f, _maxHealth);
            _current.Value = newValue;

            string verbo = amount >= 0 ? "recibió daño" : "se curó";
            Debug.Log($"[Health] {gameObject.name} {verbo} {Mathf.Abs(amount):0} → HP: {newValue:0}/{_maxHealth:0} (instigator: {instigatorNetworkId})");

            if (newValue <= 0f)
                OnDied?.Invoke(instigatorNetworkId);
        }

        /// <summary>Se dispara solo en el servidor cuando la vida llega a 0. El instigador es quién causó la muerte.</summary>
        public event System.Action<int> OnDied;
    }
}