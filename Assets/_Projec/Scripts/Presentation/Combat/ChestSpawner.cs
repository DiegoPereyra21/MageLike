using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using UnityEngine;

namespace Game.Presentation.Combat
{
    /// <summary>
    /// Puebla el mundo de cofres de loot al empezar la run y los limpia al reiniciar
    /// (server-authoritative). Consume los ChestSpawnPoint de la escena (fuente temporal;
    /// a futuro, generador procedural de mapas). Mismo rol que EnemySpawner cumple para enemigos.
    /// </summary>
    public class ChestSpawner : NetworkBehaviour
    {
        [SerializeField] private GameObject _chestPrefab;

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
            var points = FindObjectsByType<ChestSpawnPoint>(FindObjectsSortMode.None);

            foreach (var point in points)
            {
                if (_chestPrefab == null || point.LootTable == null) continue;

                GameObject chest = Instantiate(_chestPrefab, point.Position, point.Rotation);
                InstanceFinder.ServerManager.Spawn(chest);

                if (chest.TryGetComponent(out LootContainer container))
                    container.ServerFill(point.LootTable.Roll());

                if (chest.TryGetComponent(out NetworkObject nob))
                    _spawned.Add(nob);
            }

            Debug.Log($"[ChestSpawner] {_spawned.Count} cofres generados en la run.");
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