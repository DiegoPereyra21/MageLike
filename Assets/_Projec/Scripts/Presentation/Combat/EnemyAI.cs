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
        private enum State { Idle, Chase }

        [SerializeField] private float _detectionRadius = 12f;
        [SerializeField] private float _loseRadius = 16f;      // un poco mayor: histéresis para no titilar
        [SerializeField] private float _moveSpeed = 3.5f;
        [SerializeField] private float _repathInterval = 0.2f; // cada cuánto recalcula el destino

        private NavMeshAgent _agent;
        private State _state = State.Idle;
        private Transform _target;
        private float _repathTimer;
        private Health _health;

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
          // Detener la IA y despawnear el cuerpo (el LootDropper ya soltó el loot por su cuenta).
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
            if (_target == null || DistanceTo(_target) > _loseRadius)
            {
                _target = null;
                _state = State.Idle;
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

        private Transform FindNearestPlayer(float radius)
        {
            // Busca jugadores por su componente de movimiento (marca de "es un jugador").
            var players = FindObjectsByType<Game.Presentation.Player.PlayerMovementController>(FindObjectsSortMode.None);
            Transform nearest = null;
            float best = radius;
            foreach (var p in players)
            {
                float d = Vector3.Distance(transform.position, p.transform.position);
                if (d <= best)
                {
                    best = d;
                    nearest = p.transform;
                }
            }
            return nearest;
        }

        private float DistanceTo(Transform t) => Vector3.Distance(transform.position, t.position);
    }
}