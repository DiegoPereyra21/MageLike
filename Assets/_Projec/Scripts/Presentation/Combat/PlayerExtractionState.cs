using FishNet.Object;
using FishNet.Object.Synchronizing;
using Game.Core.Run;
using Game.Presentation.Player;
using UnityEngine;
using Game.Presentation.Run;

namespace Game.Presentation.Combat
{
    /// <summary>
    /// Estado de extracción del jugador (server-authoritative). Cuando el servidor lo marca
    /// extraído, se desactiva el control y se salva el loot (vía IRunInventory si existe).
    /// </summary>
    public class PlayerExtractionState : NetworkBehaviour
    {
        [SerializeField] private PlayerAvatarState _avatar;

        // Progreso de canalización [0..1], seteado por la ExtractionZone en el servidor.
        private readonly SyncVar<float> _extractionProgress = new SyncVar<float>();
        private readonly SyncVar<bool> _isExtracted = new SyncVar<bool>();

        public float ExtractionProgress => _extractionProgress.Value;
        public bool IsExtracted => _isExtracted.Value;

        /// <summary>Server-only. Llamado por la ExtractionZone cada tick con el progreso actual.</summary>
        public void ServerSetProgress(float value)
        {
            if (!base.IsServerInitialized) return;
            _extractionProgress.Value = Mathf.Clamp01(value);
        }

        /// <summary>Server-only. Marca extracción exitosa.</summary>
        public void ServerCompleteExtraction()
        {
            if (!base.IsServerInitialized) return;
            if (_isExtracted.Value) return;

            _isExtracted.Value = true;
            if (Game.Presentation.Run.RunManager.ServerInstance != null)
                Game.Presentation.Run.RunManager.ServerInstance.SetExtracted(base.ObjectId);
            _extractionProgress.Value = 1f;

            // Salvar loot (si hay inventario implementado).
            if (TryGetComponent(out IRunInventory inventory))
                inventory.CommitToStash();

            ExtractObserversRpc();
        }

        [ObserversRpc(RunLocally = true)]
        private void ExtractObserversRpc()
        {
            Debug.Log($"[Extraction] Jugador {base.ObjectId} EXTRAÍDO con éxito");
            if (_avatar != null) _avatar.DisableControl();
            // TODO: cámara spectator / pantalla de "extracción exitosa".
        }

        public override void OnStartServer()
        {
            if (Run.RunManager.ServerInstance != null)
                Run.RunManager.ServerInstance.RegisterPlayer(base.ObjectId);
        }
    }
}