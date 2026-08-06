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
            int notAdded = _inventory.TryAddItem(stack.ItemId, stack.Quantity);

            if (notAdded <= 0)
            {
                // Entró todo: despawnear el item del mundo.
                item.Despawn();
            }
            else if (notAdded < stack.Quantity)
            {
                // Entró una parte (apilable): reducir la cantidad restante en el mundo.
                item.ServerSetItem(new ItemStack(stack.ItemId, notAdded, stack.Durability));
            }
            // Si no entró nada (mochila llena), queda igual en el suelo.
        }
    }
}