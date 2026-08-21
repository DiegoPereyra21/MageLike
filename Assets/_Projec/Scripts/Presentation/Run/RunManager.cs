using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Game.Core.Run;
using Game.Presentation.Combat;
using UnityEngine;

namespace Game.Presentation.Run
{
    /// <summary>
    /// Gestor central de la partida (server-authoritative). Lleva el estado de cada jugador
    /// y determina el fin de la run. Único en la escena. Los clientes solo leen los contadores
    /// y la fase vía SyncVars.
    /// </summary>
    public class RunManager : NetworkBehaviour
    {
        /// <summary>Instancia accesible desde cualquier lado una vez que el RunManager está en la red (cliente o servidor).</summary>
        public static RunManager Instance { get; private set; }

        // Estado autoritativo por jugador (solo servidor).
        private readonly Dictionary<int, PlayerRunStatus> _statuses = new();

        // Estado sincronizado a clientes.
        private readonly SyncVar<int> _aliveCount = new SyncVar<int>();
        private readonly SyncVar<int> _extractedCount = new SyncVar<int>();
        private readonly SyncVar<int> _deadCount = new SyncVar<int>();
        private readonly SyncVar<RunPhase> _phase = new SyncVar<RunPhase>(RunPhase.InProgress);
        [SerializeField] private float _runDuration = 60f; // duración antes de la fase de peligro
        [Header("Fase de peligro")]
        [Tooltip("Daño aplicado a cada jugador vivo por cada tick, mientras dure la fase de peligro.")]
        [SerializeField] private float _dangerPhaseDamagePerTick = 120f;
        [Tooltip("Segundos entre cada tick de daño de la fase de peligro.")]
        [SerializeField] private float _dangerPhaseTickInterval = 1f;

        private float _dangerPhaseTickTimer;

        private readonly SyncVar<float> _timeRemaining = new SyncVar<float>();
        public float TimeRemaining => _timeRemaining.Value;
        /// <summary>Solo servidor. Se dispara al entrar en la fase de peligro (timer a 0).</summary>
        public event System.Action OnDangerPhaseStarted;


        public int AliveCount => _aliveCount.Value;
        public int ExtractedCount => _extractedCount.Value;
        public int DeadCount => _deadCount.Value;
        public RunPhase Phase => _phase.Value;

        /// <summary>Solo servidor. Se dispara cuando la run termina.</summary>
        public event System.Action OnRunEnded;

        public override void OnStartNetwork()
        {
            Instance = this;
        }

        public override void OnStopNetwork()
        {
            if (Instance == this) Instance = null;
        }

        public override void OnStartServer()
        {
            _statuses.Clear();
            _timeRemaining.Value = _runDuration;
            _phase.Value = RunPhase.InProgress;
            _aliveCount.Value = 0;
            _extractedCount.Value = 0;
            _deadCount.Value = 0;
        }

        public override void OnStopServer()
        {
            // La instancia se limpia en OnStopNetwork; acá no hace falta nada.
        }


        private void Update()
        {
            if (!base.IsServerInitialized) return;

            if (_phase.Value == RunPhase.InProgress)
            {
                _timeRemaining.Value -= Time.deltaTime;

                if (_timeRemaining.Value <= 0f)
                {
                    _timeRemaining.Value = 0f;
                    EnterDangerPhase();
                }
            }
            else if (_phase.Value == RunPhase.DangerPhase)
            {
                TickDangerPhaseDamage();
            }
        }

        /// <summary>Server-only. Daño ambiental parejo a todo jugador vivo, cada
        /// _dangerPhaseTickInterval segundos. Versión simplificada de la fase de peligro hasta que
        /// haya enemigos (hunters) para spawnear — ver el TODO de EnterDangerPhase.</summary>
        private void TickDangerPhaseDamage()
        {
            _dangerPhaseTickTimer += Time.deltaTime;
            if (_dangerPhaseTickTimer < _dangerPhaseTickInterval) return;
            _dangerPhaseTickTimer -= _dangerPhaseTickInterval;

            // Copia de los IDs antes de iterar: ApplyDamage puede matar al jugador y disparar en
            // cadena SetStatus() sobre este mismo diccionario, lo cual invalida el enumerador si
            // se está recorriendo _statuses directamente.
            var aliveIds = new List<int>();
            foreach (var kvp in _statuses)
                if (kvp.Value == PlayerRunStatus.Alive)
                    aliveIds.Add(kvp.Key);

            foreach (int playerObjectId in aliveIds)
            {
                if (!InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(playerObjectId, out NetworkObject nob))
                    continue;

                var health = nob.GetComponent<Health>();
                if (health == null) continue;

                health.ApplyDamage(_dangerPhaseDamagePerTick, playerObjectId);
            }
        }

        private void EnterDangerPhase()
        {
            if (_phase.Value != RunPhase.InProgress) return;

            _phase.Value = RunPhase.DangerPhase;
            _dangerPhaseTickTimer = 0f;
            OnDangerPhaseStarted?.Invoke();
            // TODO: cuando haya IA de enemigos, suscribirse a OnDangerPhaseStarted y spawnear
            //       hunters cerca de cada jugador vivo, como escalada sobre este daño ambiental
            //       (que se queda como piso mínimo de presión, no se reemplaza).
        }
        /// <summary>Server-only. Registra un jugador como vivo al entrar a la run.</summary>
        public void RegisterPlayer(int playerObjectId)
        {
            if (!base.IsServerInitialized) return;
            if (_statuses.ContainsKey(playerObjectId)) return;

            _statuses[playerObjectId] = PlayerRunStatus.Alive;
            RecountAndSync();
        }

        /// <summary>Server-only. Marca a un jugador como extraído.</summary>
        public void SetExtracted(int playerObjectId)
        {
            SetStatus(playerObjectId, PlayerRunStatus.Extracted);
        }

        /// <summary>Server-only. Marca a un jugador como muerto.</summary>
        public void SetDead(int playerObjectId)
        {
            SetStatus(playerObjectId, PlayerRunStatus.Dead);
        }

        private void SetStatus(int playerObjectId, PlayerRunStatus status)
        {
            if (!base.IsServerInitialized) return;

            if (!_statuses.ContainsKey(playerObjectId))
            {
                Debug.LogWarning($"[RunManager] SetStatus({status}) para {playerObjectId} pero NO está registrado. Registrados: {string.Join(",", _statuses.Keys)}");
                return;
            }

            if (_statuses[playerObjectId] != PlayerRunStatus.Alive)
            {
                Debug.LogWarning($"[RunManager] SetStatus({status}) para {playerObjectId} pero ya está en {_statuses[playerObjectId]}");
                return;
            }

            _statuses[playerObjectId] = status;
            RecountAndSync();
            CheckRunEnd();
        }

        private void RecountAndSync()
        {
            int alive = 0, extracted = 0, dead = 0;
            foreach (var s in _statuses.Values)
            {
                switch (s)
                {
                    case PlayerRunStatus.Alive: alive++; break;
                    case PlayerRunStatus.Extracted: extracted++; break;
                    case PlayerRunStatus.Dead: dead++; break;
                }
            }
            _aliveCount.Value = alive;
            _extractedCount.Value = extracted;
            _deadCount.Value = dead;
        }

        private void CheckRunEnd()
        {
            if (_phase.Value == RunPhase.Ended) return; // solo evita re-terminar
            if (_statuses.Count == 0) return;

            if (_aliveCount.Value <= 0)
            {
                _phase.Value = RunPhase.Ended;
                OnRunEnded?.Invoke();
            }
        }
    }
}