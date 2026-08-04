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
        /// <summary>Referencia server-side simple. En multi-instancia de runs esto se reemplaza.</summary>
        public static RunManager ServerInstance { get; private set; }

        // Estado autoritativo por jugador (solo servidor).
        private readonly Dictionary<int, PlayerRunStatus> _statuses = new();

        // Estado sincronizado a clientes.
        private readonly SyncVar<int> _aliveCount = new SyncVar<int>();
        private readonly SyncVar<int> _extractedCount = new SyncVar<int>();
        private readonly SyncVar<int> _deadCount = new SyncVar<int>();
        private readonly SyncVar<RunPhase> _phase = new SyncVar<RunPhase>(RunPhase.InProgress);

        public int AliveCount => _aliveCount.Value;
        public int ExtractedCount => _extractedCount.Value;
        public int DeadCount => _deadCount.Value;
        public RunPhase Phase => _phase.Value;

        /// <summary>Solo servidor. Se dispara cuando la run termina.</summary>
        public event System.Action OnRunEnded;

        public override void OnStartServer()
        {
            ServerInstance = this;
        }

        public override void OnStopServer()
        {
            if (ServerInstance == this) ServerInstance = null;
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
            if (!_statuses.ContainsKey(playerObjectId)) return;
            if (_statuses[playerObjectId] != PlayerRunStatus.Alive) return; // ya resuelto, no re-cambiar

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
            if (_phase.Value == RunPhase.Ended) return;
            if (_statuses.Count == 0) return;

            // Condición de fin: no queda nadie vivo (todos extraídos o muertos).
            if (_aliveCount.Value <= 0)
            {
                _phase.Value = RunPhase.Ended;
                Debug.Log($"[RunManager] Run terminada. Extraídos: {_extractedCount.Value}, Muertos: {_deadCount.Value}");
                OnRunEnded?.Invoke();
                // TODO: volver al hub, pantalla de resultados, recompensas.
            }
        }
    }
}