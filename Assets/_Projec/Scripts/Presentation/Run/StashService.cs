using System;
using System.Threading.Tasks;
using Game.Core.Items;
using Game.Core.Run;
using UnityEngine;

namespace Game.Presentation.Run
{
    /// <summary>
    /// Persiste el stash del jugador (30 slots) entre sesiones. Stash es una cache en memoria de
    /// lectura instantánea; el guardado real se delega a IStashStorage (local por defecto,
    /// PlayFab más adelante — cambiar de backend es reasignar Storage, nada más se entera).
    /// </summary>
    public static class StashService
    {
        private const int MaxSaveRetries = 3;

        /// <summary>Backend activo. Cambiar esto es todo lo que hace falta para migrar de storage.</summary>
        public static IStashStorage Storage { get; set; } = new LocalStashStorage();

        private static StashData _stash;
        private static bool _initialized;
        private static bool _pendingSync;

        /// <summary>El stash actual (cache en memoria). Null si todavía no se inicializó.</summary>
        public static StashData Stash => _stash;

        /// <summary>True si el último guardado falló tras agotar reintentos y todavía no se resincronizó.</summary>
        public static bool PendingSync => _pendingSync;

        /// <summary>
        /// Carga desde el storage si nunca se inicializó en este proceso; si no hay nada guardado
        /// (jugador nuevo), arranca con un stash vacío. Llamar una vez, del lado cliente, antes
        /// de necesitar Stash (al abrir la pantalla del menú).
        /// </summary>
        public static async Task EnsureInitializedAsync()
        {
            if (_initialized) return;

            StashData loaded = null;
            try
            {
                loaded = await Storage.LoadAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[StashService] Falló la carga inicial, arranco con stash vacío: {e.Message}");
            }

            _stash = loaded ?? new StashData();
            _initialized = true;
        }

        /// <summary>Guarda el estado actual del stash. Cache instantáneo + persistencia en background.</summary>
        public static void Save(StashData stash)
        {
            _stash = stash;
            _initialized = true;
            _ = PersistAsync(stash);
        }

        /// <summary>Reintenta persistir el estado actual si el último guardado había fallado (ej. al recuperar conexión).</summary>
        public static Task RetrySyncAsync() => PersistAsync(_stash);

        private static async Task PersistAsync(StashData stash)
        {
            for (int attempt = 1; attempt <= MaxSaveRetries; attempt++)
            {
                try
                {
                    await Storage.SaveAsync(stash);
                    _pendingSync = false;
                    return;
                }
                catch (Exception e)
                {
                    if (attempt == MaxSaveRetries)
                    {
                        _pendingSync = true;
                        Debug.LogWarning($"[StashService] No se pudo persistir tras {MaxSaveRetries} intentos (queda en cache local, se reintenta más adelante): {e.Message}");
                        return;
                    }
                    await Task.Delay(500 * attempt);
                }
            }
        }
    }
}