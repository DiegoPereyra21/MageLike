using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

namespace Game.Presentation.Bootstrap
{
    /// <summary>
    /// Login a PlayFab al arrancar el menú. Activa los backends reales de persistencia
    /// (PlayerLoadoutService/StashService) recién cuando el login resuelve — hasta entonces
    /// ambos servicios siguen en su backend Local por defecto. Vive una vez en la escena
    /// MainMenu; IsReady/OnReady son estáticos, no importa el orden de ejecución de scripts.
    /// TEMPORAL: usa LoginWithCustomID hasta tener el AppID de Steam propio. Cuando esté, este
    /// es el único archivo que hay que tocar para pasar a LoginWithSteam.
    /// </summary>
    public class PlayFabSession : MonoBehaviour
    {
        public static bool IsReady { get; private set; }
        public static event Action OnReady;

        private void Start()
        {
            if (IsReady) return; // ya logueado en este proceso (ej. volviste al MainMenu)
            _ = LoginAsync();
        }

        private async Task LoginAsync()
        {
            var tcs = new TaskCompletionSource<LoginResult>();

            var request = new LoginWithCustomIDRequest
            {
                CustomId = SystemInfo.deviceUniqueIdentifier,
                CreateAccount = true
            };

            PlayFabClientAPI.LoginWithCustomID(request,
                result => tcs.SetResult(result),
                error => tcs.SetException(new Exception(error.GenerateErrorReport())));

            try
            {
                var result = await tcs.Task;
                Debug.Log($"[PlayFabSession] Login OK. PlayFabId: {result.PlayFabId}");

                Game.Presentation.Run.PlayerLoadoutService.Storage = new Game.Presentation.Run.PlayFabPlayerLoadoutStorage();
                Game.Presentation.Run.StashService.Storage = new Game.Presentation.Run.PlayFabStashStorage();

                IsReady = true;
                OnReady?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayFabSession] Login falló, se sigue en backend local: {e.Message}");
                // No seteamos IsReady: los botones de MainMenuController quedan deshabilitados.
                // No hay reintento automático todavía — si esto molesta en playtesting (ej. sin
                // internet al abrir), el próximo paso natural es un botón de "Reintentar".
            }
        }
    }
}