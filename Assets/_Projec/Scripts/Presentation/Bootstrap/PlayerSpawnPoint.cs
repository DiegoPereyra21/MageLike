using UnityEngine;

namespace Game.Presentation.Bootstrap
{
    /// <summary>
    /// Marks a spawn location in the Run scene. Registers itself with the
    /// PlayerSpawnManager on enable so spawn points can live in a different scene
    /// than the (persistent) manager.
    /// </summary>
    public class PlayerSpawnPoint : MonoBehaviour
    {
        private void OnEnable()  => PlayerSpawnManager.Register(this);
        private void OnDisable() => PlayerSpawnManager.Unregister(this);

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
        }
#endif
    }
}