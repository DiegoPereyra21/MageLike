using System.Collections;
using FishNet;
using FishNet.Managing.Scened;
using FishNet.Transporting.Tugboat;
using UnityEngine;

namespace Game.Presentation.Bootstrap
{
    /// <summary>
    /// Arranca la red según el rol del proceso, usando Tugboat (IP directa, sin Steam).
    /// En build el rol viene por línea de comandos (-server / -client / -host, ver LaunchArgs);
    /// en el editor lo define _editorRole, que solo se usa cuando no hay argumentos.
    /// El server dedicado carga la escena de run por su cuenta: no depende de ningún menú.
    /// </summary>
    public class NetworkBootstrap : MonoBehaviour
    {
        [Header("Escena")]
        [SerializeField] private string _runSceneName = "Run";

        [Header("Editor (ignorado si el proceso arrancó con argumentos de rol)")]
        [SerializeField] private NetworkRole _editorRole = NetworkRole.None;
        [SerializeField] private string _editorAddress = "127.0.0.1";
        [SerializeField] private ushort _editorPort = 7770;

        private void Start()
        {
            NetworkRole role = LaunchArgs.Role;
            string address = LaunchArgs.Address;
            ushort port = LaunchArgs.Port;

            // Sin argumentos de rol (caso típico del editor): usar la configuración del inspector.
            if (role == NetworkRole.None)
            {
                role = _editorRole;
                address = _editorAddress;
                port = _editorPort;
            }

            if (role == NetworkRole.None)
                return; // arranque normal por menú, la red la levanta otra cosa

            ConfigureTransport(address, port);
            StartRole(role);
        }

        /// <summary>Aplica dirección y puerto al transporte activo antes de conectar.</summary>
        private void ConfigureTransport(string address, ushort port)
        {
            var tugboat = InstanceFinder.TransportManager.GetTransport<Tugboat>();
            if (tugboat == null)
            {
                Debug.LogError("[NetworkBootstrap] No se encontró el transporte Tugboat en el NetworkManager.");
                return;
            }

            tugboat.SetPort(port);
            tugboat.SetClientAddress(address);
        }

        private void StartRole(NetworkRole role)
        {
            switch (role)
            {
                case NetworkRole.Server:
                    InstanceFinder.ServerManager.StartConnection();
                    StartCoroutine(LoadRunWhenServerReady());
                    break;

                case NetworkRole.Client:
                    InstanceFinder.ClientManager.StartConnection();
                    break;

                case NetworkRole.Host:
                    InstanceFinder.ServerManager.StartConnection();
                    InstanceFinder.ClientManager.StartConnection();
                    StartCoroutine(LoadRunWhenServerReady());
                    break;
            }
        }

        /// <summary>Carga la run como escena global apenas el servidor está levantado. Esperar
        /// únicamente por IsServerStarted es deliberado: agregar IsClientStarted acá ya causó
        /// spawns por debajo del mapa.</summary>
        private IEnumerator LoadRunWhenServerReady()
        {
            while (!InstanceFinder.IsServerStarted) yield return null;

            SceneLoadData sld = new SceneLoadData(_runSceneName);
            sld.ReplaceScenes = ReplaceOption.All;
            InstanceFinder.SceneManager.LoadGlobalScenes(sld);
        }
    }
}