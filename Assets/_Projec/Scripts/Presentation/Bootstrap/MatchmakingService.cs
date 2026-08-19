using System;
using System.Collections;
using PlayFab;
using PlayFab.MultiplayerModels;
using UnityEngine;

namespace Game.Presentation.Bootstrap
{
    /// <summary>
    /// Cola de matchmaking de PlayFab. Emite un ticket, lo poletea hasta que hay match (o falla),
    /// y expone el estado para que la UI lo dibuje. No sabe nada de UI ni de red de juego: quien
    /// escuche OnStateChanged decide qué hacer cuando el estado llega a Matched.
    /// </summary>
    public class MatchmakingService : MonoBehaviour
    {
        public enum State
        {
            Idle = 0,
            Searching = 1,
            Matched = 2,
            Failed = 3,
        }

        [SerializeField] private string _queueName = "SpellHaulRaid";
        [Tooltip("Segundos que PlayFab intenta emparejar el ticket antes de darlo por vencido.")]
        [SerializeField] private int _giveUpAfterSeconds = 120;
        [Tooltip("PlayFab permite hasta 10 consultas por minuto: no bajar de 6 segundos.")]
        [SerializeField] private float _pollIntervalSeconds = 6f;

        public State CurrentState { get; private set; } = State.Idle;

        /// <summary>Id del match encontrado. Solo válido en estado Matched.</summary>
        public string MatchId { get; private set; }

        /// <summary>Detalles del servidor asignado. Null si la queue no tiene server allocation
        /// (hoy: siempre null, se resuelve al desplegar el Build en MPS).</summary>
        public ServerDetails ServerDetails { get; private set; }

        public event Action<State> OnStateChanged;

        /// <summary>Motivo del último fallo, para mostrarle algo útil al jugador.</summary>
        public event Action<string> OnFailed;

        private string _ticketId;
        private Coroutine _pollRoutine;

        /// <summary>Entra en la cola. No hace nada si ya está buscando.</summary>
        public void StartSearch()
        {
            if (CurrentState == State.Searching) return;

            if (string.IsNullOrEmpty(PlayFabSession.EntityId))
            {
                Fail("La sesión de PlayFab todavía no está lista.");
                return;
            }

            MatchId = null;
            ServerDetails = null;
            _ticketId = null;
            SetState(State.Searching);

            var request = new CreateMatchmakingTicketRequest
            {
                    Creator = new MatchmakingPlayer
                {
                    Entity = new EntityKey
                    {
                        Id = PlayFabSession.EntityId,
                        Type = PlayFabSession.EntityType,
                    },
                    // Sin atributos: la queue no tiene reglas. Cuando se agreguen (skill, región),
                    // hay que mandar Attributes con DataObject — nunca un objeto vacío, PlayFab
                    // lo rechaza porque exige DataObject o EscapedDataObject, uno de los dos.
                },
                GiveUpAfterSeconds = _giveUpAfterSeconds,
                QueueName = _queueName,
            };

            PlayFabMultiplayerAPI.CreateMatchmakingTicket(request, OnTicketCreated, OnApiError);
        }

        /// <summary>Sale de la cola. Cancela el ticket en PlayFab si había uno emitido.</summary>
        public void CancelSearch()
        {
            if (CurrentState != State.Searching) return;

            StopPolling();

            if (!string.IsNullOrEmpty(_ticketId))
            {
                PlayFabMultiplayerAPI.CancelMatchmakingTicket(
                    new CancelMatchmakingTicketRequest
                    {
                        QueueName = _queueName,
                        TicketId = _ticketId,
                    },
                    _ => { },
                    error => Debug.LogWarning($"[Matchmaking] No se pudo cancelar el ticket: {error.GenerateErrorReport()}"));
            }

            _ticketId = null;
            SetState(State.Idle);
        }

        private void OnTicketCreated(CreateMatchmakingTicketResult result)
        {
            _ticketId = result.TicketId;
            Debug.Log($"[Matchmaking] Ticket creado: {_ticketId}");
            _pollRoutine = StartCoroutine(PollTicketRoutine());
        }

        private IEnumerator PollTicketRoutine()
        {
            var wait = new WaitForSeconds(_pollIntervalSeconds);

            while (CurrentState == State.Searching)
            {
                yield return wait;
                if (CurrentState != State.Searching) yield break;

                PlayFabMultiplayerAPI.GetMatchmakingTicket(
                    new GetMatchmakingTicketRequest
                    {
                        QueueName = _queueName,
                        TicketId = _ticketId,
                    },
                    OnTicketPolled,
                    OnApiError);
            }
        }

        private void OnTicketPolled(GetMatchmakingTicketResult result)
        {
            if (CurrentState != State.Searching) return;

            switch (result.Status)
            {
                // Estados intermedios: seguir esperando.
                case "WaitingForPlayers":
                case "WaitingForMatch":
                case "WaitingForServer":
                    break;

                case "Matched":
                    MatchId = result.MatchId;
                    StopPolling();
                    FetchMatchDetails();
                    break;

                case "Canceled":
                    StopPolling();
                    // Sin razón explícita = se agotó GiveUpAfterSeconds (no había con quién emparejar).
                    string reason = string.IsNullOrEmpty(result.CancellationReasonString)
                        ? "No se encontró partida a tiempo."
                        : result.CancellationReasonString;
                    Fail(reason);
                    break;

                default:
                    Debug.LogWarning($"[Matchmaking] Estado de ticket no contemplado: {result.Status}");
                    break;
            }
        }

        /// <summary>Con el match confirmado, pide los detalles (incluye el servidor asignado si la
        /// queue tiene server allocation).</summary>
        private void FetchMatchDetails()
        {
            PlayFabMultiplayerAPI.GetMatch(
                new GetMatchRequest
                {
                    MatchId = MatchId,
                    QueueName = _queueName,
                },
                result =>
                {
                    ServerDetails = result.ServerDetails;
                    int playerCount = result.Members?.Count ?? 0;
                    Debug.Log($"[Matchmaking] Match encontrado. MatchId: {MatchId} | Jugadores: {playerCount} | ServerDetails: {(ServerDetails == null ? "null (sin server allocation)" : ServerDetails.IPV4Address)}");
                    SetState(State.Matched);
                },
                OnApiError);
        }

        private void StopPolling()
        {
            if (_pollRoutine != null)
            {
                StopCoroutine(_pollRoutine);
                _pollRoutine = null;
            }
        }

        private void OnApiError(PlayFabError error)
        {
            StopPolling();
            Fail(error.GenerateErrorReport());
        }

        private void Fail(string reason)
        {
            Debug.LogError($"[Matchmaking] {reason}");
            SetState(State.Failed);
            OnFailed?.Invoke(reason);
        }

        private void SetState(State newState)
        {
            if (CurrentState == newState) return;
            CurrentState = newState;
            OnStateChanged?.Invoke(newState);
        }

        private void OnDisable()
        {
            StopPolling();
        }
    }
}