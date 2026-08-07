using FishNet.Object;
using Game.Core.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.Combat
{
    /// <summary>
    /// Detecta con qué WorldItem está apuntando el jugador (raycast desde la cámara), muestra
    /// el prompt y, con la tecla, pide recogerlo. La recogida real es server-authoritative.
    /// </summary>
    public class PlayerInteraction : NetworkBehaviour
    {
        [SerializeField] private Transform _aimOrigin;      // la cámara
        [SerializeField] private float _range = 3.5f;
        [SerializeField] private LayerMask _interactMask;   // capa de los WorldItem
        [SerializeField] private RunInventory _inventory;
        [SerializeField] private ItemDatabase _database;
        [SerializeField] private Game.Presentation.UI.InventoryUIController _inventoryUI;
        private LootContainer _currentContainer;
        private InputAction _interactAction;
        private WorldItem _current;   // a qué estamos apuntando ahora

        /// <summary>El HUD lee esto para el prompt. Null = no apuntando a nada recogible.</summary>
        public string CurrentPrompt { get; private set; }

        private bool _inputBlocked;
        public void SetInputBlocked(bool blocked) => _inputBlocked = blocked;

        private void Awake()
        {
            _interactAction = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
        }

        private void OnEnable() => _interactAction.Enable();
        private void OnDisable() => _interactAction.Disable();

        private void Update()
        {
            if (!base.IsOwner) return;
            if (_inputBlocked) { CurrentPrompt = null; return; }

            DetectTarget();

            if (_interactAction.WasPressedThisFrame())
            {
                if (_current != null)
                    PickupServerRpc(_current);
                else if (_currentContainer != null && _inventoryUI != null)
                    _inventoryUI.OpenWithContainer(_currentContainer);
            }
        }

        private void DetectTarget()
        {
            _current = null;
            _currentContainer = null;
            CurrentPrompt = null;

            if (_aimOrigin == null) return;

            if (Physics.Raycast(_aimOrigin.position, _aimOrigin.forward, out RaycastHit hit, _range, _interactMask))
            {
                // ¿Es un item suelto?
                WorldItem item = hit.collider.GetComponentInParent<WorldItem>();
                if (item != null)
                {
                    _current = item;
                    CurrentPrompt = $"E  Recoger {item.GetDisplayName(_database)} x{item.Quantity}";
                    return;
                }

                // ¿Es un contenedor (cadáver)?
                LootContainer container = hit.collider.GetComponentInParent<LootContainer>();
                if (container != null && !container.IsEmpty)
                {
                    _currentContainer = container;
                    CurrentPrompt = "E  Saquear";
                    return;
                }
            }
        }

        [ServerRpc]
        private void PickupServerRpc(WorldItem item)
        {
            if (item == null) return;

            ItemStack stack = item.ToStack();
            ItemSO def = _database.GetById(stack.ItemId);

            // ¿Es una mochila? Intentar auto-equip si conviene.
            if (def is EquipmentItemSO equip && equip.Slot == EquipmentSlot.Backpack)
            {
                int equipSlot = (int)EquipmentSlot.Backpack;
                ItemStack current = _inventory.Equipment[equipSlot];

                // Calcular capacidad actual vs nueva.
                int currentCap = 0;
                if (!current.IsEmpty && _database.GetById(current.ItemId) is EquipmentItemSO currentEquip)
                    currentCap = currentEquip.BackpackSlots;

                if (equip.BackpackSlots > currentCap)
                {
                    // Equipar la nueva mochila directamente.
                    _inventory.EquipBackpackFromWorld(stack);

                    // La mochila anterior (si había) va al suelo.
                    if (!current.IsEmpty)
                        SpawnWorldItemNear(current);

                    item.Despawn();
                    return;
                }
                // Si no es mejor, cae al flujo normal (intentar meter a mochila).
            }

            // Flujo normal: intentar agregar a la mochila.
            int notAdded = _inventory.TryAddItem(stack.ItemId, stack.Quantity);

            if (notAdded <= 0)
                item.Despawn();
            else if (notAdded < stack.Quantity)
                item.ServerSetItem(new ItemStack(stack.ItemId, notAdded, stack.Durability));
        }

        private void SpawnWorldItemNear(ItemStack stack)
        {
            // Reutiliza el mismo prefab y lógica que RunInventory, pero desde PlayerInteraction.
            // Delegamos al RunInventory para no duplicar la lógica de spawn.
            _inventory.SpawnWorldItemPublic(stack, transform.position);
        }
    }
}