using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;

namespace Game.Presentation.Bootstrap
{
    /// <summary>
    /// Spawns each connecting player at a random registered PlayerSpawnPoint.
    /// Server-authoritative. Spawn points register themselves from the Run scene.
    /// </summary>
    public class PlayerSpawnManager : MonoBehaviour
    {
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private NetworkObject _playerPrefab;

        private static readonly List<PlayerSpawnPoint> _points = new();
        private readonly List<int> _bag = new();

        public static void Register(PlayerSpawnPoint p)
        {
            if (!_points.Contains(p)) _points.Add(p);
        }

        public static void Unregister(PlayerSpawnPoint p)
        {
            _points.Remove(p);
        }

        private void Start()
        {
            if (_networkManager == null)
                _networkManager = FishNet.InstanceFinder.NetworkManager;

            _networkManager.SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes;
        }

        private void OnDestroy()
        {
            if (_networkManager != null)
                _networkManager.SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;
        }

        private void OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
        {
            if (!asServer) return;
            if (_playerPrefab == null) return;

            Transform point = PickSpawnPoint();
            Vector3 pos = point != null ? point.position : Vector3.zero;
            Quaternion rot = point != null ? point.rotation : Quaternion.identity;

            NetworkObject nob = _networkManager.GetPooledInstantiated(_playerPrefab, pos, rot, true);
            _networkManager.ServerManager.Spawn(nob, conn);
        }

        private Transform PickSpawnPoint()
        {
            if (_points.Count == 0) return null;

            if (_bag.Count == 0)
                for (int i = 0; i < _points.Count; i++)
                    _bag.Add(i);

            int pick = Random.Range(0, _bag.Count);
            int index = _bag[pick];
            _bag.RemoveAt(pick);

            index = Mathf.Clamp(index, 0, _points.Count - 1); // safety if points changed
            return _points[index].transform;
        }
    }
}