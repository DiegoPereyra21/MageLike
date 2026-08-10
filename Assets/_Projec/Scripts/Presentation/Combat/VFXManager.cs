using Game.Core.Pooling;
using UnityEngine;

namespace Game.Presentation.Combat
{
    public class VFXManager : MonoBehaviour
    {
        private static VFXManager _instance;

        [Header("Proyectil básico")]
        [SerializeField] private ParticleSystem _projectileHitPrefab;
        [SerializeField] private int _projectileHitPoolSize = 8;

        [Header("Orbe explosivo")]
        [SerializeField] private ParticleSystem _orbExplosionPrefab;
        [SerializeField] private int _orbExplosionPoolSize = 4;

        [Header("Muzzle proyectil básico")]
        [SerializeField] private ParticleSystem _projectileMuzzlePrefab;
        [SerializeField] private int _projectileMuzzlePoolSize = 8;

        [Header("Parry")]
        [SerializeField] private ParticleSystem _parryActivePrefab;
        [SerializeField] private int _parryActivePoolSize = 4;
        [SerializeField] private ParticleSystem _parrySuccessPrefab;
        [SerializeField] private int _parrySuccessPoolSize = 4;

        private ObjectPool<ParticleSystem> _projectileHitPool;
        private ObjectPool<ParticleSystem> _orbExplosionPool;
        private ObjectPool<ParticleSystem> _projectileMuzzlePool;
        private ObjectPool<ParticleSystem> _parryActivePool;
        private ObjectPool<ParticleSystem> _parrySuccessPool;

        private void Awake()
        {
            if (_instance != null) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            if (_projectileHitPrefab != null)
                _projectileHitPool = new ObjectPool<ParticleSystem>(_projectileHitPrefab, _projectileHitPoolSize, transform);
            if (_orbExplosionPrefab != null)
                _orbExplosionPool = new ObjectPool<ParticleSystem>(_orbExplosionPrefab, _orbExplosionPoolSize, transform);
            if (_projectileMuzzlePrefab != null)
                _projectileMuzzlePool = new ObjectPool<ParticleSystem>(_projectileMuzzlePrefab, _projectileMuzzlePoolSize, transform);
            if (_parryActivePrefab != null)
                _parryActivePool = new ObjectPool<ParticleSystem>(_parryActivePrefab, _parryActivePoolSize, transform);
            if (_parrySuccessPrefab != null)
                _parrySuccessPool = new ObjectPool<ParticleSystem>(_parrySuccessPrefab, _parrySuccessPoolSize, transform);
        }

        public static void PlayProjectileHit(Vector3 point, Quaternion rotation)
            => _instance?.PlayVFX(_instance._projectileHitPool, point, rotation);

        public static void PlayOrbExplosion(Vector3 point)
            => _instance?.PlayVFX(_instance._orbExplosionPool, point, Quaternion.identity);

        public static void PlayProjectileMuzzle(Vector3 point, Quaternion rotation)
            => _instance?.PlayVFX(_instance._projectileMuzzlePool, point, rotation);

        public static void PlayParryActive(Vector3 point, float scale = 1f)
            => _instance?.PlayVFXScaled(_instance._parryActivePool, point, Quaternion.identity, scale);

        public static void PlayParrySuccess(Vector3 point, float scale = 1f)
            => _instance?.PlayVFXScaled(_instance._parrySuccessPool, point, Quaternion.identity, scale);

        private void PlayVFXScaled(ObjectPool<ParticleSystem> pool, Vector3 point, Quaternion rotation, float scale)
        {
            if (pool == null) return;
            ParticleSystem ps = pool.Get(point, rotation);

            // Forzar que el ParticleSystem (y sus hijos) respeten la escala del transform.
            ApplyHierarchyScaling(ps);
            ps.transform.localScale = Vector3.one * scale;

            ps.Play();
            StartCoroutine(ReleaseWhenDone(ps, pool));
        }

        private void ApplyHierarchyScaling(ParticleSystem root)
        {
            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
        }

        private void PlayVFX(ObjectPool<ParticleSystem> pool, Vector3 point, Quaternion rotation)
        {
            if (pool == null) return;
            ParticleSystem ps = pool.Get(point, rotation);
            ps.Play();
            StartCoroutine(ReleaseWhenDone(ps, pool));
        }

        private System.Collections.IEnumerator ReleaseWhenDone(ParticleSystem ps, ObjectPool<ParticleSystem> pool)
        {
            yield return new WaitUntil(() => !ps.isPlaying);
            ps.transform.localScale = Vector3.one; // resetear para el próximo uso del pool
            pool.Release(ps);
        }
    }
}