using System.Collections.Generic;
using Game.Core.Pooling;
using UnityEngine;

namespace Game.Presentation.Abilities
{
    /// <summary>
    /// Pool de proyectiles cosméticos locales, uno por prefab (data-driven). Se auto-crea la
    /// primera vez que se lo usa, así no hace falta agregarlo a la escena. Reutiliza vía
    /// ObjectPool en lugar de Instantiate/Destroy.
    /// </summary>
    public class CosmeticProjectileManager : MonoBehaviour
    {
        private static CosmeticProjectileManager _instance;
        private readonly Dictionary<GameObject, ObjectPool<CosmeticProjectile>> _pools = new();

        private static CosmeticProjectileManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("CosmeticProjectileManager");
                    _instance = go.AddComponent<CosmeticProjectileManager>();
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        public static void Spawn(GameObject prefab, Vector3 position, Vector3 direction, float speed, Transform ignoreRoot)
        {
            if (prefab == null) return;
            Instance.SpawnInternal(prefab, position, direction, speed, ignoreRoot);
        }

        private void SpawnInternal(GameObject prefab, Vector3 position, Vector3 direction, float speed, Transform ignoreRoot)
        {
            if (!prefab.TryGetComponent(out CosmeticProjectile prefabComp))
            {
                Debug.LogWarning($"[CosmeticProjectileManager] El prefab '{prefab.name}' no tiene CosmeticProjectile.");
                return;
            }

            if (!_pools.TryGetValue(prefab, out ObjectPool<CosmeticProjectile> pool))
            {
                pool = new ObjectPool<CosmeticProjectile>(prefabComp, 0, transform);
                _pools[prefab] = pool;
            }

            CosmeticProjectile cp = pool.Get(position, Quaternion.LookRotation(direction));
            cp.Launch(direction, speed, ignoreRoot, () => pool.Release(cp));
        }
    }
}