using FishNet.Object;
using Game.Presentation.Abilities;
using Game.Presentation.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation.UI
{
    /// <summary>
    /// HUD del jugador local: barra de vida + cooldowns. Puramente cliente/visual,
    /// lee el estado ya sincronizado (Health SyncVar, cooldowns locales del AbilityController).
    /// Va en el prefab Player, junto a un UIDocument.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HUDController : NetworkBehaviour
    {
        private UIDocument _document;
        private Health _health;
        private Mana _mana;
        private PlayerExtractionState _extraction;
        private AbilityController _abilities;

        private VisualElement _healthFill;
        private VisualElement _manaFill;
        private Label _manaText;
        private Label _healthText;
        //para la extraccion
        private VisualElement _extractionContainer;
        private VisualElement _extractionFill;
        private Label _extractionLabel;
        private Label _runTimer;
        private Label _runDanger;
        private Label _runCounter;
        private PlayerInteraction _interaction;
        private Label _interactPrompt;
        private readonly VisualElement[] _cooldownOverlays = new VisualElement[4];

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            _health = GetComponent<Health>();
            _mana = GetComponent<Mana>();
            _extraction = GetComponent<PlayerExtractionState>();
            _abilities = GetComponent<AbilityController>();
            _interaction = GetComponent<PlayerInteraction>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Solo el dueño ve su propio HUD. Los demás Players no deben renderizar UI.
            if (!base.IsOwner)
            {
                _document.rootVisualElement.style.display = DisplayStyle.None;
                enabled = false;
                return;
            }

            var root = _document.rootVisualElement;
            _healthFill = root.Q<VisualElement>("health-bar-fill");
            _healthText = root.Q<Label>("health-text");
            _manaFill = root.Q<VisualElement>("mana-bar-fill");
            _manaText = root.Q<Label>("mana-text");
            _extractionContainer = root.Q<VisualElement>("extraction-container");
            _extractionFill = root.Q<VisualElement>("extraction-bar-fill");
            _extractionLabel = root.Q<Label>("extraction-label");
            _runTimer = root.Q<Label>("run-timer");
            _runDanger = root.Q<Label>("run-danger");
            _runCounter = root.Q<Label>("run-counter");
            _interactPrompt = root.Q<Label>("interact-prompt");

            for (int i = 0; i < 4; i++)
                _cooldownOverlays[i] = root.Q<VisualElement>($"slot-{i}-cd");
        }

        private void Update()
        {
            if (!base.IsOwner) return;

            // Vida
            if (_health != null && _healthFill != null)
            {
                float pct = _health.Max > 0 ? _health.Current / _health.Max : 0f;
                _healthFill.style.width = Length.Percent(pct * 100f);
                _healthText.text = $"{_health.Current:0} / {_health.Max:0}";
            }

            // Maná
            if (_mana != null && _manaFill != null)
            {
                float pct = _mana.Max > 0 ? _mana.Current / _mana.Max : 0f;
                _manaFill.style.width = Length.Percent(pct * 100f);
                _manaText.text = $"{_mana.Current:0} / {_mana.Max:0}";
            }
            
            // Cooldowns
            if (_abilities != null)
            {
                for (int i = 0; i < _cooldownOverlays.Length; i++)
                {
                    if (_cooldownOverlays[i] == null) continue;
                    float cd = _abilities.GetCooldownNormalized(i);
                    _cooldownOverlays[i].style.height = Length.Percent(cd * 100f);
                }
            }

            // Extracción
            if (_extraction != null && _extractionContainer != null)
            {
                float progress = _extraction.ExtractionProgress;
                bool channeling = progress > 0f && !_extraction.IsExtracted;

                if (_extraction.IsExtracted)
                {
                    _extractionContainer.style.display = DisplayStyle.Flex;
                    _extractionFill.style.width = Length.Percent(100f);
                    _extractionLabel.text = "Extraído";
                }
                else if (channeling)
                {
                    _extractionContainer.style.display = DisplayStyle.Flex;
                    _extractionFill.style.width = Length.Percent(progress * 100f);
                    _extractionLabel.text = "Extrayendo...";
                }
                else
                {
                    _extractionContainer.style.display = DisplayStyle.None;
                }
            }

            // Info de run
            var run = Game.Presentation.Run.RunManager.Instance;
            if (run != null && _runTimer != null)
            {
                int total = Mathf.CeilToInt(run.TimeRemaining);
                int mm = total / 60;
                int ss = total % 60;
                _runTimer.text = $"{mm:00}:{ss:00}";

                bool danger = run.Phase == Game.Core.Run.RunPhase.DangerPhase;
                _runDanger.style.display = danger ? DisplayStyle.Flex : DisplayStyle.None;

                _runCounter.text = $"Vivos: {run.AliveCount}  ·  Extraídos: {run.ExtractedCount}  ·  Muertos: {run.DeadCount}";
            }

            //interact prompt
            if (_interaction != null && _interactPrompt != null)
            {
                string prompt = _interaction.CurrentPrompt;
                if (!string.IsNullOrEmpty(prompt))
                {
                    _interactPrompt.style.display = DisplayStyle.Flex;
                    _interactPrompt.text = prompt;
                }
                else
                {
                    _interactPrompt.style.display = DisplayStyle.None;
                }
            }
        }
    }
}