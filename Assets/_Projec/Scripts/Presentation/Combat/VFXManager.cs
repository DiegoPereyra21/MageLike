using Game.Core.Pooling;
using UnityEngine;

namespace Game.Presentation.Combat
{
    public class VFXManager : MonoBehaviour
    {
        private static VFXManager _instance;

        [Header("Impacto proyectil básico")]
        [SerializeField] private ParticleSystem _projectileHitPrefab;
        [SerializeField] private int _projectileHitPoolSize = 8;

        [Header("Explosión orbe")]
        [SerializeField] private ParticleSystem _orbExplosionPrefab;
        [SerializeField] private int _orbExplosionPoolSize = 4;

        [Header("Muzzle proyectil básico")]
        [SerializeField] private ParticleSystem _projectileMuzzlePrefab;
        [SerializeField] private int _projectileMuzzlePoolSize = 8;

        private ObjectPool<ParticleSystem> _projectileHitPool;
        private ObjectPool<ParticleSystem> _orbExplosionPool;
        private ObjectPool<ParticleSystem> _projectileMuzzlePool;

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
        }

        public static void PlayProjectileHit(Vector3 point, Quaternion rotation)
            => _instance?.PlayVFX(_instance._projectileHitPool, point, rotation);

        public static void PlayOrbExplosion(Vector3 point)
            => _instance?.PlayVFX(_instance._orbExplosionPool, point, Quaternion.identity);

        public static void PlayProjectileMuzzle(Vector3 point, Quaternion rotation)
            => _instance?.PlayVFX(_instance._projectileMuzzlePool, point, rotation);

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
            pool.Release(ps);
        }
    }
}