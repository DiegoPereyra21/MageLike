using System.Collections;
using UnityEngine;

namespace Game.Presentation.Combat
{
    public class ScreenShake : MonoBehaviour
    {
        private static ScreenShake _instance;

        private void Awake() => _instance = this;

        public static void Shake(float force = 1f, float duration = 0.2f)
        {
            if (_instance == null) return;
            _instance.StopAllCoroutines();
            _instance.StartCoroutine(_instance.DoShake(force, duration));
        }

        private IEnumerator DoShake(float force, float duration)
        {
            Vector3 originalPos = transform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = 1f - (elapsed / duration); // amortiguado
                float offsetX = Random.Range(-1f, 1f) * force * t * 0.1f;
                float offsetY = Random.Range(-1f, 1f) * force * t * 0.1f;
                transform.localPosition = originalPos + new Vector3(offsetX, offsetY, 0f);
                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.localPosition = originalPos;
        }
    }
}