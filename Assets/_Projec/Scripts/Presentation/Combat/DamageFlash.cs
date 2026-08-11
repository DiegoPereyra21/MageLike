using System.Collections;
using UnityEngine;

namespace Game.Presentation.Combat
{
    /// <summary>
    /// Parpadeo de daño visible para todos. Es puramente cliente/visual, pero lo dispara
    /// el servidor desde Health.ApplyDamage vía ObserversRpc (nunca local-only).
    /// Usa MaterialPropertyBlock: no instancia materiales (sin leak, respeta batching).
    /// Soporta dos modos sin reescribir nada: tinte de color base (robusto, default) o
    /// pulso de emisión (más vistoso, requiere material con Emission activada + Bloom).
    /// </summary>
    public class DamageFlash : MonoBehaviour
    {
        public enum FlashMode { BaseColorTint, EmissionPulse }

        [Header("Renderers (vacío = autocompleta hijos)")]
        [SerializeField] private Renderer[] _renderers;

        [Header("Modo")]
        [SerializeField] private FlashMode _mode = FlashMode.EmissionPulse;

        [Header("Look")]
        [SerializeField] private Color _flashColor = Color.white;
        [Tooltip("Intensidad del tinte en el pico (0..1). Solo modo BaseColorTint.")]
        [Range(0f, 1f)]
        [SerializeField] private float _tintStrength = 0.85f;
        [Tooltip("Multiplicador HDR de la emisión en el pico (>1 para glow). Solo modo EmissionPulse.")]
        [SerializeField] private float _emissionIntensity = 3f;
        [SerializeField] private float _duration = 0.12f;

        [Header("Propiedades de shader (URP Lit por defecto)")]
        [Tooltip("Built-in Standard usa \"_Color\" en vez de \"_BaseColor\".")]
        [SerializeField] private string _baseColorProperty = "_BaseColor";
        [SerializeField] private string _emissionProperty = "_EmissionColor";

        private int _baseColorId;
        private int _emissionId;
        private MaterialPropertyBlock _mpb;
        private Coroutine _routine;
        private Color[] _baseColors;
        private Color[] _baseEmissions;

        private void Awake()
        {
            if (_renderers == null || _renderers.Length == 0)
                _renderers = GetComponentsInChildren<Renderer>(true);

            _baseColorId = Shader.PropertyToID(_baseColorProperty);
            _emissionId = Shader.PropertyToID(_emissionProperty);
            _mpb = new MaterialPropertyBlock();
            CacheBaseline();
        }

        private void CacheBaseline()
        {
            _baseColors = new Color[_renderers.Length];
            _baseEmissions = new Color[_renderers.Length];

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                Material m = r != null ? r.sharedMaterial : null;

                _baseColors[i] = (m != null && m.HasProperty(_baseColorId))
                    ? m.GetColor(_baseColorId) : Color.white;
                _baseEmissions[i] = (m != null && m.HasProperty(_emissionId))
                    ? m.GetColor(_emissionId) : Color.black;
            }
        }

        /// <summary>Cliente. Dispara el parpadeo; si ya estaba corriendo, reinicia.</summary>
        public void Play()
        {
            if (_renderers == null || _renderers.Length == 0) return;
            if (!isActiveAndEnabled) return;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            float t = 0f;
            while (t < _duration)
            {
                Apply(1f - (t / _duration)); // pico al inicio, decae
                t += Time.deltaTime;
                yield return null;
            }
            Apply(0f); // volver al baseline
            _routine = null;
        }

        private void Apply(float k)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null) continue;

                r.GetPropertyBlock(_mpb);

                if (_mode == FlashMode.BaseColorTint)
                {
                    Color c = Color.Lerp(_baseColors[i], _flashColor, k * _tintStrength);
                    _mpb.SetColor(_baseColorId, c);
                }
                else
                {
                    Color c = Color.Lerp(_baseEmissions[i], _flashColor * _emissionIntensity, k);
                    _mpb.SetColor(_emissionId, c);
                }

                r.SetPropertyBlock(_mpb);
            }
        }
    }
}