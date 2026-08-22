using System.Collections.Generic;

namespace Game.Presentation.Player
{
    /// <summary>
    /// Registro server-side de los PlayerMovementController activos en la run. Reemplaza los
    /// FindObjectsByType&lt;PlayerMovementController&gt;() que hacía cada IA de enemigo por
    /// frame (escaneo de escena + allocation de array nueva, cada frame, por cada enemigo sin
    /// target). Solo tiene sentido en el servidor — los clientes no llaman a esto.
    /// </summary>
    public static class PlayerRegistry
    {
        private static readonly List<PlayerMovementController> _active = new List<PlayerMovementController>();

        public static IReadOnlyList<PlayerMovementController> Active => _active;

        public static void Register(PlayerMovementController player)
        {
            if (!_active.Contains(player))
                _active.Add(player);
        }

        public static void Unregister(PlayerMovementController player)
        {
            _active.Remove(player);
        }
    }
}