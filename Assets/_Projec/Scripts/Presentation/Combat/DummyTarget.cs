using UnityEngine;

namespace Game.Presentation.Combat
{
    /// <summary>
    /// Blanco de pruebas: muestra su vida actual en pantalla (debug) para validar
    /// que daño de área, proyectil y cura funcionan. No hace nada más.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class DummyTarget : MonoBehaviour
    {
        private Health _health;
        private Camera _cam;

        private void Awake()
        {
            _health = GetComponent<Health>();
        }

        private void OnGUI()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            Vector3 screen = _cam.WorldToScreenPoint(transform.position + Vector3.up * 1.5f);
            if (screen.z < 0) return;

            GUI.color = Color.red;
            GUI.Label(new Rect(screen.x - 30, Screen.height - screen.y, 120, 20),
                $"HP: {_health.Current:0}/{_health.Max:0}");
        }
    }
}