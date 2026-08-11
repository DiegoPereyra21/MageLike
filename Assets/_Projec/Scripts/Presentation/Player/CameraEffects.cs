using System.Collections;
using UnityEngine;

namespace Game.Presentation.Player
{
    /// <summary>
    /// Local (owner-only) camera juice: FOV kicks, etc. Purely visual, never networked.
    /// Lives on the same GameObject as the player Camera.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraEffects : MonoBehaviour
    {
        private Camera _camera;
        private float _baseFov;
        private Coroutine _fovRoutine;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _baseFov = _camera.fieldOfView;
        }

        /// <summary>
        /// Kicks the FOV outward then eases it back to the base value.
        /// </summary>
        /// <param name="amount">Degrees added to the base FOV at the peak.</param>
        /// <param name="inDuration">Seconds to reach the peak.</param>
        /// <param name="outDuration">Seconds to return to base.</param>
        public void FovKick(float amount = 12f, float inDuration = 0.08f, float outDuration = 0.25f)
        {
            if (_fovRoutine != null) StopCoroutine(_fovRoutine);
            _fovRoutine = StartCoroutine(FovKickRoutine(amount, inDuration, outDuration));
        }

        private IEnumerator FovKickRoutine(float amount, float inDuration, float outDuration)
        {
            float targetFov = _baseFov + amount;

            // Ease out to peak.
            float t = 0f;
            float start = _camera.fieldOfView;
            while (t < inDuration)
            {
                t += Time.deltaTime;
                _camera.fieldOfView = Mathf.Lerp(start, targetFov, Mathf.SmoothStep(0f, 1f, t / inDuration));
                yield return null;
            }
            _camera.fieldOfView = targetFov;

            // Ease back to base.
            t = 0f;
            while (t < outDuration)
            {
                t += Time.deltaTime;
                _camera.fieldOfView = Mathf.Lerp(targetFov, _baseFov, Mathf.SmoothStep(0f, 1f, t / outDuration));
                yield return null;
            }
            _camera.fieldOfView = _baseFov;
        }
    }
}