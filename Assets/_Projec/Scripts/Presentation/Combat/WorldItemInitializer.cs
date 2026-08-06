using FishNet.Object;
using Game.Core.Items;
using UnityEngine;

namespace Game.Presentation.Combat
{
    /// <summary>
    /// Para WorldItems colocados a mano en la escena: al iniciar el servidor, configura
    /// qué item y cantidad representan. Solo para pruebas / loot pre-colocado en el mapa.
    /// </summary>
    [RequireComponent(typeof(WorldItem))]
    public class WorldItemInitializer : NetworkBehaviour
    {
        [SerializeField] private ItemSO _item;
        [SerializeField] private int _quantity = 1;

        private WorldItem _worldItem;

        private void Awake()
        {
            _worldItem = GetComponent<WorldItem>();
        }

        public override void OnStartServer()
        {
            if (_item != null)
                _worldItem.ServerSetItem(new ItemStack(_item.ItemId, _quantity, 1f));
        }
    }
}