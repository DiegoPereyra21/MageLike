using UnityEngine;

namespace Game.Presentation.Abilities
{
    /// <summary>
    /// Efecto de carga (telegrafía) que crece mientras se sostiene un cast: windup con
    /// temporizador fijo, o una habilidad de carga sostenida (mantener y soltar). Puramente
    /// visual/cliente; vive como hijo fijo del SpellOrigin (no pooled: 1 por jugador,
    /// siempre presente, igual que el trail del dash).
    /// </summary>
    public class ChargeVFXController : MonoBehaviour
    {
        [SerializeField] private GameObject _visual; // partícula o mesh a mostrar mientras carga
        [SerializeField] private float _minScale = 0.15f;
        [SerializeField] private float _maxScale = 1f;

        private Coroutine _routine;

        private void Awake()
        {
            if (_visual != null) _visual.SetActive(false);
        }

        /// <summary>Empieza a crecer hasta maxDuration segundos. Al llegar al máximo se queda ahí (no se auto-dispara).</summary>
        public void BeginCharge(float maxDuration)
        {
            if (_routine != null) StopCoroutine(_routine);
            if (_visual != null) _visual.SetActive(true);
            _routine = StartCoroutine(GrowRoutine(Mathf.Max(0.01f, maxDuration)));
        }

        /// <summary>Corta la carga ya (soltó, o terminó el windup). Sin fade largo: es feedback rápido.</summary>
        public void EndCharge()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            if (_visual != null) _visual.SetActive(false);
        }

        private System.Collections.IEnumerator GrowRoutine(float maxDuration)
        {
            float t = 0f;
            while (t < maxDuration)
            {
                float k = t / maxDuration;
                if (_visual != null) _visual.transform.localScale = Vector3.one * Mathf.Lerp(_minScale, _maxScale, k);
                t += Time.deltaTime;
                yield return null;
            }
            if (_visual != null) _visual.transform.localScale = Vector3.one * _maxScale;
        }
    }
}