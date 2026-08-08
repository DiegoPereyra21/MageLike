using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Managing.Scened;
using Steamworks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string _runSceneName = "Run";
        [SerializeField] private StashScreenController _stashScreen;

        private UIDocument _document;
        private VisualElement _lobbyPanel;
        private ScrollView _lobbyList;

        // Callbacks de Steam (hay que guardar referencia para que no sean GC'd)
        private CallResult<LobbyCreated_t> _lobbyCreated;
        private CallResult<LobbyMatchList_t> _lobbyMatchList;
        private Callback<LobbyEnter_t> _lobbyEnter;

        private CSteamID _joinedLobby;

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            var root = _document.rootVisualElement;

            root.Q<Button>("host-button").clicked  += OnHostClicked;
            root.Q<Button>("join-button").clicked  += OnJoinClicked;
            root.Q<Button>("stash-button").clicked += () => { if (_stashScreen != null) _stashScreen.Show(); };
            root.Q<Button>("quit-button").clicked  += () => Application.Quit();
            root.Q<Button>("lobby-cancel").clicked += () => SetLobbyPanel(false);

            _lobbyPanel = root.Q<VisualElement>("lobby-panel");
            _lobbyList  = root.Q<ScrollView>("lobby-list");

            // Registrar callbacks de Steam.
            _lobbyCreated   = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
            _lobbyMatchList = CallResult<LobbyMatchList_t>.Create(OnLobbyMatchList);
            _lobbyEnter     = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
        }

        // ---------- Host ----------

        private void OnHostClicked()
        {
            // Crear lobby público con máximo 6 jugadores.
            var handle = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 6);
            _lobbyCreated.Set(handle);
        }

        private void OnLobbyCreated(LobbyCreated_t result, bool failure)
        {
            if (failure || result.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogError("[Steam] Error al crear el lobby.");
                return;
            }

            CSteamID lobbyId = new CSteamID(result.m_ulSteamIDLobby);

            // Guardar el SteamID del host en los metadatos del lobby para que los clientes puedan conectarse.
            SteamMatchmaking.SetLobbyData(lobbyId, "hostSteamId",
                SteamUser.GetSteamID().ToString());
            SteamMatchmaking.SetLobbyData(lobbyId, "gameName", "MageLike");

            Debug.Log($"[Steam] Lobby creado: {lobbyId}");

            // Arrancar server+client y cargar la run.
            InstanceFinder.ServerManager.StartConnection();
            InstanceFinder.ClientManager.StartConnection();
            StartCoroutine(LoadRunWhenReady());
        }

        // ---------- Join ----------

        private void OnJoinClicked()
        {
            SetLobbyPanel(true);
            _lobbyList.Clear();

            // Filtrar lobbies de MageLike y pedirla lista.
            SteamMatchmaking.AddRequestLobbyListStringFilter("gameName", "MageLike",
                ELobbyComparison.k_ELobbyComparisonEqual);
            var handle = SteamMatchmaking.RequestLobbyList();
            _lobbyMatchList.Set(handle);
        }

        private void OnLobbyMatchList(LobbyMatchList_t result, bool failure)
        {
            if (failure) { Debug.LogError("[Steam] Error al buscar lobbies."); return; }

            if (result.m_nLobbiesMatching == 0)
            {
                var empty = new Label("No hay partidas disponibles.");
                empty.AddToClassList("lobby-entry");
                _lobbyList.Add(empty);
                return;
            }

            for (int i = 0; i < result.m_nLobbiesMatching; i++)
            {
                CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);
                string hostId    = SteamMatchmaking.GetLobbyData(lobbyId, "hostSteamId");
                int members      = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
                int maxMembers   = SteamMatchmaking.GetLobbyMemberLimit(lobbyId);

                var entry = new Label($"Partida  {members}/{maxMembers} jugadores");
                entry.AddToClassList("lobby-entry");

                CSteamID capturedLobby = lobbyId;
                entry.RegisterCallback<ClickEvent>(_ => JoinLobby(capturedLobby, hostId));
                _lobbyList.Add(entry);
            }
        }

        private void JoinLobby(CSteamID lobbyId, string hostSteamId)
        {
            _joinedLobby = lobbyId;
            SteamMatchmaking.JoinLobby(lobbyId);
            // OnLobbyEnter se dispara cuando Steam confirma la entrada.
        }

        private void OnLobbyEnter(LobbyEnter_t result)
        {
            if (result.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Debug.LogError("[Steam] No se pudo entrar al lobby.");
                return;
            }

            CSteamID lobbyId  = new CSteamID(result.m_ulSteamIDLobby);
            string hostSteamId = SteamMatchmaking.GetLobbyData(lobbyId, "hostSteamId");

            if (string.IsNullOrEmpty(hostSteamId))
            {
                Debug.LogError("[Steam] No se encontró el SteamID del host.");
                return;
            }

            SetLobbyPanel(false);

            // Conectarse al host por Steam P2P Relay.
            InstanceFinder.ClientManager.StartConnection(hostSteamId);
        }

        // ---------- Helpers ----------

        private void SetLobbyPanel(bool visible)
            => _lobbyPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        private IEnumerator LoadRunWhenReady()
        {
            while (!InstanceFinder.IsServerStarted) yield return null;

            SceneLoadData sld = new SceneLoadData(_runSceneName);
            sld.ReplaceScenes = ReplaceOption.All;
            InstanceFinder.SceneManager.LoadGlobalScenes(sld);
        }
    }
}