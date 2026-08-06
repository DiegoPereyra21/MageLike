using UnityEngine;

namespace Game.Presentation.Combat
{
    /// <summary>
    /// Marcador de posición donde nace un enemigo, con el tipo que le corresponde.
    /// Fuente temporal: hoy se colocan a mano en la escena; en el futuro, el generador
    /// procedural de mapas produciría estas posiciones. El EnemySpawner los consume.
    /// </summary>
    public class EnemySpawnPoint : MonoBehaviour
    {
        [SerializeField] private GameObject _enemyPrefab;

        public GameObject EnemyPrefab => _enemyPrefab;
        public Vector3 Position => transform.position;
        public Quaternion Rotation => transform.rotation;

        // Dibuja un marcador en el Editor para verlos sin darle Play.
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward);
        }
    }
}