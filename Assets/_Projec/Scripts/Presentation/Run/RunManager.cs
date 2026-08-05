using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Game.Core.Run;
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
        [SerializeField] private float _runDuration = 12f; // duración antes de la fase de peligro
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
            if (_phase.Value != RunPhase.InProgress) return; // el timer solo corre en la fase inicial

            _timeRemaining.Value -= Time.deltaTime;

            if (_timeRemaining.Value <= 0f)
            {
                _timeRemaining.Value = 0f;
                EnterDangerPhase();
            }
        }

        private void EnterDangerPhase()
        {
            if (_phase.Value != RunPhase.InProgress) return;

            _phase.Value = RunPhase.DangerPhase;
            Debug.Log("[RunManager] ¡Fase de peligro iniciada! (aquí spawnearán los hunters cuando exista la IA)");
            OnDangerPhaseStarted?.Invoke();
            // TODO: al implementar IA de enemigos, suscribirse a OnDangerPhaseStarted y spawnear hunters
            //       cerca de cada jugador vivo.
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
                Debug.Log($"[RunManager] Run terminada. Extraídos: {_extractedCount.Value}, Muertos: {_deadCount.Value}");
                OnRunEnded?.Invoke();
            }
        }
    }
}