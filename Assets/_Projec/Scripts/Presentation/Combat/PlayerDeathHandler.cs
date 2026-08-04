using FishNet.Object;
using UnityEngine;
using Game.Presentation.Player;

namespace Game.Presentation.Combat
{
    /// <summary>
    /// Maneja la muerte del jugador en un extraction: sin respawn. Al morir, el jugador
    /// queda eliminado de la run. Por ahora: desactiva control y colisión, y avisa al dueño.
    /// (Feedback visual / cámara spectator se agregan después.)
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerDeathHandler : NetworkBehaviour
    {
        [SerializeField] private MonoBehaviour[] _componentsToDisableOnDeath; // movimiento, habilidades, etc.
        [SerializeField] private Collider _bodyCollider;
        [SerializeField] private PlayerMovementController _movement;

        private Health _health;

        private void Awake()
        {
            _health = GetComponent<Health>();
        }

        public override void OnStartServer()
        {
            _health.OnDied += HandleDiedServer;
        }

        public override void OnStopServer()
        {
            if (_health != null) _health.OnDied -= HandleDiedServer;
        }

        private void HandleDiedServer(int instigatorNetworkId)
        {
            Debug.Log($"[Death] Jugador {base.ObjectId} eliminado (por {instigatorNetworkId})");
            // Propaga el efecto de muerte a todos (desactivar control/colisión).
            DieObserversRpc();
        }

        [ObserversRpc(RunLocally = true)]
        private void DieObserversRpc()
        {
          if (_movement != null)
          _movement.DisableMovement();
          
            // Desactivar componentes de control en todas las instancias.
            if (_componentsToDisableOnDeath != null)
            {
                foreach (var c in _componentsToDisableOnDeath)
                    if (c != null) c.enabled = false;
            }

            if (_bodyCollider != null)
                _bodyCollider.enabled = false;

            // TODO: activar cámara spectator para el dueño, mostrar pantalla de eliminación,
            // soltar loot, marcar como "fuera de la run" en el estado de la partida.
        }
    }
}