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

          float newValue = Mathf.Clamp(_current.Value - amount, 0f, _maxHealth);
          _current.Value = newValue;

          string verbo = amount >= 0 ? "recibió daño" : "se curó";
          Debug.Log($"[Health] {gameObject.name} {verbo} {Mathf.Abs(amount):0} → HP: {newValue:0}/{_maxHealth:0} (instigator: {instigatorNetworkId})");

          if (newValue <= 0f)
               OnDeath();
          }

        private void OnDeath()
        {
            // Placeholder: por ahora solo despawnea. Más adelante: ragdoll, drop de loot,
            // respawn, evento de kill para scoring, etc.
            base.Despawn();
        }
    }
}