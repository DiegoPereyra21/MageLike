using FishNet;
using FishNet.Managing.Scened;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation.UI
{
    /// <summary>
    /// Pantalla de resultados de la run (Extraído / Eliminado). La dispara el estado individual
    /// del jugador (muerte o extracción). El botón vuelve al menú (host local por ahora).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ResultScreenController : MonoBehaviour
    {
        [SerializeField] private string _menuSceneName = "MainMenu";

        private UIDocument _document;
        private VisualElement _root;
        private Label _title;
        private Label _subtitle;

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            _root = _document.rootVisualElement.Q<VisualElement>("result-root");
            _title = _root.Q<Label>("result-title");
            _subtitle = _root.Q<Label>("result-subtitle");

            var returnBtn = _root.Q<Button>("return-button");
            if (returnBtn != null)
                returnBtn.clicked += OnReturnClicked;
        }

        /// <summary>Muestra la pantalla con el resultado. extracted=true si extrajo, false si murió.</summary>
        public void Show(bool extracted)
        {
            _root.style.display = DisplayStyle.Flex;

            _title.RemoveFromClassList("extracted");
            _title.RemoveFromClassList("died");

            if (extracted)
            {
                _title.text = "EXTRAÍDO";
                _title.AddToClassList("extracted");
                _subtitle.text = "Sobreviviste. Tu botín está a salvo.";
            }
            else
            {
                _title.text = "ELIMINADO";
                _title.AddToClassList("died");
                _subtitle.text = "Caíste en la run. Perdiste lo que llevabas.";
            }

            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        private void OnReturnClicked()
        {
            // Cerrar la conexión y volver al menú (host local).
            if (InstanceFinder.IsServerStarted) InstanceFinder.ServerManager.StopConnection(true);
            if (InstanceFinder.IsClientStarted) InstanceFinder.ClientManager.StopConnection();

            // Cargar la escena de menú localmente (ya no hay red que sincronizar).
            UnityEngine.SceneManagement.SceneManager.LoadScene(_menuSceneName);
        }
    }
}