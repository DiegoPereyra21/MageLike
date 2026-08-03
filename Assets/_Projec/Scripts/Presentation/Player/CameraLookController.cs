using UnityEngine;

namespace Game.Presentation.Player
{
    /// <summary>
    /// Aplica el pitch (rotación vertical) a la cámara. El input NO se lee acá:
    /// lo alimenta PlayerMovementController desde el tick de red, para que pitch y yaw
    /// vayan al mismo ritmo y se sientan idénticos (independiente del framerate).
    /// </summary>
    public class CameraLookController : MonoBehaviour
    {
        [SerializeField] private float _minPitch = -80f;
        [SerializeField] private float _maxPitch = 80f;

        private float _pitch;

        public void AddPitch(float pitchDelta)
        {
            _pitch = Mathf.Clamp(_pitch + pitchDelta, _minPitch, _maxPitch);
            transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }
}