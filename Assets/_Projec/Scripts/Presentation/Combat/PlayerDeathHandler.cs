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
        [SerializeField] private Game.Presentation.Player.PlayerAvatarState _avatar;

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
            if (_avatar != null)
                _avatar.DisableControl();

            // Soltar el loot de la run al morir (si hay inventario implementado).
            if (TryGetComponent(out Game.Core.Run.IRunInventory inventory))
                inventory.DropAll();

            // TODO: cámara spectator, pantalla de eliminación.
        }
    }
}