using FishNet.Object;
using UnityEngine;

namespace Game.Presentation.Combat
{
    /// <summary>
    /// Mata a la entidad (vía Health) si cae por debajo de un umbral de altura. Server-only.
    /// Reutilizable: ponelo en cualquier prefab con Health que deba morir al caerse del mapa.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class FallDeath : NetworkBehaviour
    {
        [Tooltip("Altura mínima (Y de mundo). Por debajo de esto, la entidad muere. " +
                 "Ponelo bien debajo del punto más bajo transitable del mapa.")]
        [SerializeField] private float _killFloorY = -20f;

        private Health _health;

        private void Awake() => _health = GetComponent<Health>();

        private void Update()
        {
            if (!base.IsServerInitialized) return;
            if (_health == null || _health.IsDead) return;

            if (transform.position.y < _killFloorY)
            {
                // Daño masivo: garantiza muerte pese a la reducción por protección, y reusa
                // todo el flujo existente (OnDied → muerte / extracción / loot / despawn).
                // Instigador = la propia entidad (caída, sin crédito de kill a otro jugador).
                _health.ApplyDamage(_health.Max * 1000f, base.ObjectId);
            }
        }
    }
}