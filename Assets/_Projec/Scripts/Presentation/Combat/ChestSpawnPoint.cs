using Game.Core.Items;
using UnityEngine;

namespace Game.Presentation.Combat
{
    /// <summary>
    /// Marcador de posición donde nace un cofre de loot, con la tabla que le corresponde.
    /// Fuente temporal: hoy se colocan a mano en la escena; en el futuro, el generador
    /// procedural de mapas produciría estas posiciones. El ChestSpawner los consume.
    /// </summary>
    public class ChestSpawnPoint : MonoBehaviour
    {
        [SerializeField] private LootTableSO _lootTable;

        public LootTableSO LootTable => _lootTable;
        public Vector3 Position => transform.position;
        public Quaternion Rotation => transform.rotation;

        // Dibuja un marcador en el Editor para verlos sin darle Play.
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.6f);
        }
    }
}