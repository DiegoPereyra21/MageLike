using Game.Presentation.Combat;
using UnityEngine;

namespace Game.Presentation.Abilities
{
    /// <summary>
    /// Proyectil puramente visual y LOCAL (sin red, sin daño). Lo spawnea el cliente del
    /// tirador al castear, para feedback instantáneo sin esperar el RTT del proyectil networked.
    /// Frena en geometría/objetivos con un raycast local e ignora al propio caster. El daño real
    /// lo decide siempre el proyectil server-authoritative.
    /// </summary>
    public class CosmeticProjectile : MonoBehaviour
    {
        [Tooltip("Contra qué frena visualmente. Hitbox + Ground. Si queda vacío se resuelve por nombre.")]
        [SerializeField] private LayerMask _hitMask;
        [SerializeField] private float _lifetime = 5f;

        private Vector3 _direction;
        private float _speed;
        private Transform _ignoreRoot;
        private System.Action _onDone;
        private float _aliveTime;
        private bool _active;

        private static readonly RaycastHit[] _hits = new RaycastHit[8];

        private void Awake()
        {
            if (_hitMask.value == 0)
                _hitMask = LayerMask.GetMask("Hitbox", "Ground");
        }

        public void Launch(Vector3 direction, float speed, Transform ignoreRoot, System.Action onDone)
        {
            _direction = direction.normalized;
            _speed     = speed;
            _ignoreRoot = ignoreRoot;
            _onDone    = onDone;
            _aliveTime = 0f;
            _active    = true;
            transform.rotation = Quaternion.LookRotation(_direction);
        }

        private void Update()
        {
            if (!_active) return;

            float step = _speed * Time.deltaTime;
            Vector3 pos = transform.position;

            if (TryHit(pos, step, out Vector3 point, out Vector3 normal))
            {
                transform.position = point;
                VFXManager.PlayProjectileHit(point, Quaternion.LookRotation(normal));
                ScreenShake.Shake(0.8f); // feedback de impacto del caster, instantáneo (local)
                Stop();
                return;
            }

            transform.position = pos + _direction * step;

            _aliveTime += Time.deltaTime;
            if (_aliveTime >= _lifetime) Stop();
        }

        private bool TryHit(Vector3 from, float distance, out Vector3 point, out Vector3 normal)
        {
            point = default; normal = default;
            int count = Physics.RaycastNonAlloc(from, _direction, _hits, distance, _hitMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = _hits[i];
                if (_ignoreRoot != null && h.collider.transform.IsChildOf(_ignoreRoot)) continue; // ignorar al propio caster
                if (h.distance < best) { best = h.distance; point = h.point; normal = h.normal; found = true; }
            }
            return found;
        }

        private void Stop()
        {
            _active = false;
            _onDone?.Invoke(); // devolver al pool
        }
    }
}