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
        private AbilityController _abilities;

        private VisualElement _healthFill;
        private VisualElement _manaFill;
        private Label _manaText;
        private Label _healthText;
        private readonly VisualElement[] _cooldownOverlays = new VisualElement[4];

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            _health = GetComponent<Health>();
            _mana = GetComponent<Mana>();
            _abilities = GetComponent<AbilityController>();
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
        }
    }
}