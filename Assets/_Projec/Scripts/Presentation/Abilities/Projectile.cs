using FishNet.Object;
using Game.Core.Abilities;
using UnityEngine;

namespace Game.Presentation.Abilities
{
    /// <summary>
    /// Proyectil simple: el servidor mueve y detecta colisión, el cliente solo visualiza
    /// (NetworkTransform en el prefab se encarga de interpolar la posición sincronizada).
    /// Prefab debe tener NetworkObject con "Enable Pooling" activado.
    /// </summary>
    public class Projectile : NetworkBehaviour
    {
        [SerializeField] private float _lifetime = 5f;

        private Vector3 _direction;
        private float _speed;
        private float _damage;
        private int _casterNetworkId;
        private float _spawnTime;

        public void Initialize(Vector3 direction, float speed, float damage, int casterNetworkId)
        {
            _direction = direction;
            _speed = speed;
            _damage = damage;
            _casterNetworkId = casterNetworkId;
        }

        public override void OnStartServer()
        {
            _spawnTime = Time.time;
        }

        private void Update()
        {
            if (!base.IsServerStarted) return;

            transform.position += _direction * _speed * Time.deltaTime;

            if (Time.time - _spawnTime >= _lifetime)
                base.Despawn();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!base.IsServerStarted) return;

            if (other.TryGetComponent(out IDamageable damageable))
                damageable.ApplyDamage(_damage, _casterNetworkId);

            base.Despawn();
        }
    }
}
