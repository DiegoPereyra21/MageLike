using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

namespace Game.Presentation.Bootstrap
{
    /// <summary>Test temporal de conectividad con PlayFab. Borrar una vez confirmado el login.</summary>
    public class PlayFabConnectionTest : MonoBehaviour
    {
        private void Start()
        {
            var request = new LoginWithCustomIDRequest
            {
                CustomId = SystemInfo.deviceUniqueIdentifier,
                CreateAccount = true
            };

            PlayFabClientAPI.LoginWithCustomID(request, OnLoginSuccess, OnLoginFailure);
        }

        private void OnLoginSuccess(LoginResult result)
        {
            Debug.Log($"[PlayFab] Login OK. PlayFabId: {result.PlayFabId}");
        }

        private void OnLoginFailure(PlayFabError error)
        {
            Debug.LogError($"[PlayFab] Login falló: {error.GenerateErrorReport()}");
        }
    }
}