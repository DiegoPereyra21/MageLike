using FishNet.Object;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Presentation.Combat
{
    /// <summary>
    /// IA de enemigo básica, server-authoritative. FSM mínima: Idle (quieto) y Chase (persigue
    /// al jugador más cercano dentro del radio de detección). El NavMeshAgent solo se mueve en
    /// el servidor; el NetworkTransform replica la posición a los clientes.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyAI : NetworkBehaviour
    {
        private enum State { Idle, Chase, Attack }

        [SerializeField] private float _detectionRadius = 12f;
        [SerializeField] private float _loseRadius = 16f;      // un poco mayor: histéresis para no titilar
        [SerializeField] private float _moveSpeed = 3.5f;
        [SerializeField] private float _repathInterval = 0.2f; // cada cuánto recalcula el destino
        [Header("Attack")]
        [SerializeField] private float _attackRange = 2f;
        [SerializeField] private float _attackWindup = 0.4f;   // telegrafía antes del golpe
        [SerializeField] private float _attackCooldown = 1.5f; // tiempo entre golpes
        [SerializeField] private float _attackDamage = 15f;

        private NavMeshAgent _agent;
        private State _state = State.Idle;
        private Transform _target;
        private float _repathTimer;
        private Health _health;
        private float _attackTimer;   // cooldown entre ataques
        private float _windupTimer;   // cuenta regresiva del windup
        private bool _winding;        // está preparando un golpe

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
        }

        public override void OnStartServer()
        {
            _agent.speed = _moveSpeed;
            _agent.enabled = true; // el agent solo vive en el servidor
            _health.OnDied += HandleDied;
        }

        public override void OnStartClient()
        {
            // En clientes puros (no host), el agent no debe controlar la posición:
            // la posición llega por NetworkTransform. Desactivarlo si no somos servidor.
            if (!base.IsServerInitialized)
                _agent.enabled = false;
        }

        public override void OnStopServer()
        {
            if (_health != null) _health.OnDied -= HandleDied;
        }

        private void HandleDied(int instigator)
        {
            _state = State.Idle;
            if (_agent != null && _agent.isOnNavMesh) _agent.ResetPath();
            Despawn();
        }

        private void Update()
        {
            if (!base.IsServerInitialized) return; // la IA corre SOLO en el servidor

            switch (_state)
            {
                case State.Idle: TickIdle(); break;
                case State.Chase: TickChase(); break;
                case State.Attack: TickAttack(); break;
            }
        }

        private void TickIdle()
        {
            Transform nearest = FindNearestPlayer(_detectionRadius);
            if (nearest != null)
            {
                _target = nearest;
                _state = State.Chase;
            }
        }

        private void TickChase()
        {
            if (!TargetIsValid() || DistanceTo(_target) > _loseRadius)
            {
                _target = null;
                _state = State.Idle;
                if (_agent.isOnNavMesh) _agent.ResetPath();
                return;
            }

            // Si está en rango de ataque, pasar a Attack.
            if (DistanceTo(_target) <= _attackRange)
            {
                _state = State.Attack;
                _winding = false;
                if (_agent.isOnNavMesh) _agent.ResetPath();
                return;
            }

            _repathTimer -= Time.deltaTime;
            if (_repathTimer <= 0f)
            {
                _repathTimer = _repathInterval;
                if (_agent.isOnNavMesh)
                    _agent.SetDestination(_target.position);
            }
        }

        private void TickAttack()
        {
            // Si el objetivo se fue o se alejó del rango de ataque, volver a perseguir.
            if (!TargetIsValid() || DistanceTo(_target) > _attackRange)
            {
                _winding = false;
                _state = _target != null ? State.Chase : State.Idle;
                return;
            }

            // Mirar hacia el objetivo (rotación plana, sin inclinarse).
            Vector3 dir = _target.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);

            _attackTimer -= Time.deltaTime;

            if (!_winding)
            {
                // Iniciar un golpe si el cooldown lo permite.
                if (_attackTimer <= 0f)
                {
                    _winding = true;
                    _windupTimer = _attackWindup;
                }
            }
            else
            {
                // En windup: contar hasta el impacto.
                _windupTimer -= Time.deltaTime;
                if (_windupTimer <= 0f)
                {
                    _winding = false;
                    _attackTimer = _attackCooldown;
                    TryLandHit();
                }
            }
        }

        private Transform FindNearestPlayer(float radius)
        {
            var players = FindObjectsByType<Game.Presentation.Player.PlayerMovementController>(FindObjectsSortMode.None);
            Transform nearest = null;
            float best = radius;
            foreach (var p in players)
            {
                if (p.TryGetComponent(out Health h) && h.IsDead) continue;
                if (p.TryGetComponent(out Game.Presentation.Combat.PlayerExtractionState ext) && ext.IsExtracted) continue;

                float d = Vector3.Distance(transform.position, p.transform.position);
                if (d <= best)
                {
                    best = d;
                    nearest = p.transform;
                }
            }
            return nearest;
        }

        private bool TargetIsValid()
        {
            if (_target == null) return false;
            if (_target.TryGetComponent(out Health h) && h.IsDead) return false;

            // Ignorar jugadores que ya extrajeron (salieron de la run).
            if (_target.TryGetComponent(out Game.Presentation.Combat.PlayerExtractionState ext) && ext.IsExtracted)
                return false;

            return true;
        }

        private void TryLandHit()
        {
            if (!TargetIsValid() || DistanceTo(_target) > _attackRange) return;

            if (_target.TryGetComponent(out Game.Core.Abilities.IDamageable dmg))
                dmg.ApplyDamage(_attackDamage, base.ObjectId);
        }

        private float DistanceTo(Transform t) => Vector3.Distance(transform.position, t.position);
    }
}