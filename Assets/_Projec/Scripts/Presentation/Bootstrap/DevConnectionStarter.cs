using FishNet;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.Bootstrap
{
    public class DevConnectionStarter : MonoBehaviour
    {
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
            {
                InstanceFinder.ServerManager.StartConnection();
                InstanceFinder.ClientManager.StartConnection();
            }
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(10, 10, 300, 20), "Presioná H para iniciar Host (server+client)");
        }
    }
}