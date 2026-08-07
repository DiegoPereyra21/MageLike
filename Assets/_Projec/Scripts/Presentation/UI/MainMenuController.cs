using FishNet;
using FishNet.Managing.Scened;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation.UI
{
    /// <summary>
    /// Menú principal. "Comenzar Partida" inicia el host local (server + client) y carga la
    /// escena de Run a través del SceneManager de Fish-Net (sincronizado). Host local por ahora;
    /// UGS Relay/Lobby se enchufa aquí después sin cambiar el flujo.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string _runSceneName = "Run";

        private UIDocument _document;

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            var root = _document.rootVisualElement;

            var startBtn = root.Q<Button>("start-button");
            Debug.Log($"[MainMenu] start-button encontrado: {startBtn != null}");
            if (startBtn != null)
                startBtn.clicked += OnStartClicked;

            var quitBtn = root.Q<Button>("quit-button");
            if (quitBtn != null)
                quitBtn.clicked += OnQuitClicked;
        }

        private void OnStartClicked()
        {
            Debug.Log("[MainMenu] Comenzar Partida clickeado");
            InstanceFinder.ServerManager.StartConnection();
            InstanceFinder.ClientManager.StartConnection();
            StartCoroutine(LoadRunWhenReady());
        }

        private System.Collections.IEnumerator LoadRunWhenReady()
        {
            while (!InstanceFinder.IsServerStarted)
                yield return null;

            Debug.Log("[MainMenu] Server iniciado, cargando escena de Run");
            SceneLoadData sld = new SceneLoadData(_runSceneName);
            sld.ReplaceScenes = ReplaceOption.All;
            InstanceFinder.SceneManager.LoadGlobalScenes(sld);
        }

        private void OnQuitClicked()
        {
            Application.Quit();
        }
    }
}