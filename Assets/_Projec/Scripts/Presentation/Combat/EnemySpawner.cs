using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using UnityEngine;

namespace Game.Presentation.Combat
{
    /// <summary>
    /// Puebla el mundo de enemigos al empezar la run y los limpia al reiniciar (server-authoritative).
    /// Consume los EnemySpawnPoint de la escena (fuente temporal; a futuro, generador procedural).
    /// Pieza definitiva: su rol no cambia aunque cambie de dónde vienen las posiciones.
    /// </summary>
    public class EnemySpawner : NetworkBehaviour
    {
        private readonly List<NetworkObject> _spawned = new List<NetworkObject>();

        public override void OnStartServer()
        {
            SpawnAll();
        }

        public override void OnStopServer()
        {
            DespawnAll();
        }

        private void SpawnAll()
        {
            // Recolectar todos los puntos de spawn de la escena.
            var points = FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None);

            foreach (var point in points)
            {
                if (point.EnemyPrefab == null) continue;

                GameObject enemy = Instantiate(point.EnemyPrefab, point.Position, point.Rotation);
                InstanceFinder.ServerManager.Spawn(enemy);

                if (enemy.TryGetComponent(out NetworkObject nob))
                    _spawned.Add(nob);
            }

            Debug.Log($"[EnemySpawner] {_spawned.Count} enemigos generados en la run.");
        }

        private void DespawnAll()
        {
            foreach (var nob in _spawned)
            {
                if (nob != null && nob.IsSpawned)
                    nob.Despawn();
            }
            _spawned.Clear();
        }
    }
}