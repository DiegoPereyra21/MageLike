using UnityEngine;

namespace Game.Presentation.Abilities
{
    /// <summary>
    /// Dibuja una previsualización client-only de la trayectoria balística de un orbe cargado,
    /// mientras se mantiene presionado el botón. Puramente visual/predictivo — usa la misma
    /// fórmula que el vuelo real (ChargedOrbProjectile), así que nunca "miente" sobre cómo va
    /// a volar. El servidor sigue siendo quien mide la carga real y valida el disparo.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class TrajectoryPreviewController : MonoBehaviour
    {
        [SerializeField] private LineRenderer _line;
        [SerializeField] private int _pointCount = 24;

        private void Awake()
        {
            if (_line == null) _line = GetComponent<LineRenderer>();
            if (_line != null) _line.enabled = false;
        }

        public void Hide()
        {
            if (_line != null) _line.enabled = false;
        }

        /// <summary>
        /// Simula la misma física real (lanzamiento directo + gravedad, sin resolver hacia atrás)
        /// paso a paso, y corta la línea aproximadamente donde tocaría el piso.
        /// </summary>
        public void Show(Vector3 origin, Vector3 aimPoint, float launchSpeed, float gravity)
        {
            if (_line == null || launchSpeed <= 0f) return;
            _line.enabled = true;

            Vector3 direction = aimPoint - origin;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            Vector3 velocity = direction * launchSpeed;

            const float dt = 0.05f;
            Vector3 pos = origin;
            Vector3 vel = velocity;
            int count = 1;
            _line.positionCount = _pointCount;
            _line.SetPosition(0, pos);

            for (int i = 1; i < _pointCount; i++)
            {
                vel.y += gravity * dt;
                pos += vel * dt;
                _line.SetPosition(i, pos);
                count = i + 1;
                if (pos.y <= origin.y - 0.05f) break; // aprox. tocó el piso a la altura de lanzamiento
            }

            _line.positionCount = count;
        }
    }
}