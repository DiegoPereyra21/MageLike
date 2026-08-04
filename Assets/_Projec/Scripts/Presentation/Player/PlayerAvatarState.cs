using FishNet.Object;
using Game.Presentation.Abilities;
using UnityEngine;

namespace Game.Presentation.Player
{
    /// <summary>
    /// Estado del avatar del jugador y lógica común de "desactivar control", compartida
    /// entre muerte y extracción. Evita duplicar la desactivación en cada handler.
    /// </summary>
    public class PlayerAvatarState : NetworkBehaviour
    {
        [SerializeField] private PlayerMovementController _movement;
        [SerializeField] private AbilityController _abilities;
        [SerializeField] private MonoBehaviour[] _extraToDisable; // cámara, etc.
        [SerializeField] private Collider _bodyCollider;

        private bool _controlDisabled;

        /// <summary>
        /// Desactiva todo el control del avatar. Idempotente (llamar dos veces no hace daño).
        /// Corre en todas las instancias (via el RPC del handler que lo llame).
        /// </summary>
        public void DisableControl()
        {
            if (_controlDisabled) return;
            _controlDisabled = true;

            if (_movement != null) _movement.DisableMovement();
            if (_abilities != null) _abilities.enabled = false;

            if (_extraToDisable != null)
                foreach (var c in _extraToDisable)
                    if (c != null) c.enabled = false;

            if (_bodyCollider != null) _bodyCollider.enabled = false;
        }
    }
}