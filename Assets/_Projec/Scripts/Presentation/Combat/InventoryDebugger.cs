using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.Combat
{
    /// <summary>
    /// Andamio de pruebas del inventario (se borra después). Teclas:
    /// F1 = agregar item de prueba, F2 = equipar slot 0 de mochila, F3 = log del inventario.
    /// </summary>
    public class InventoryDebugger : NetworkBehaviour
    {
        [SerializeField] private RunInventory _inventory;
        [SerializeField] private string _testItemId = "black_wood";
        [SerializeField] private int _testQuantity = 3;

        private void Update()
        {
            if (!base.IsOwner || Keyboard.current == null) return;

            if (Keyboard.current.f1Key.wasPressedThisFrame) AddServerRpc(_testItemId, _testQuantity);
            if (Keyboard.current.f2Key.wasPressedThisFrame) EquipServerRpc(0);
            if (Keyboard.current.f3Key.wasPressedThisFrame) LogServerRpc();
        }

        [ServerRpc] private void AddServerRpc(string id, int qty) => _inventory.TryAddItem(id, qty);
        [ServerRpc] private void EquipServerRpc(int index) => _inventory.TryEquipFromInventory(1, index);
        [ServerRpc] private void LogServerRpc() => _inventory.DebugLogContents();
    }
}