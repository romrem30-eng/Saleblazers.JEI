using System;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Saleblazers.ModBase;

internal static class ModService
{
    public static HRGameManager GameManager =>
        BaseGameManager.Get != null ? BaseGameManager.Get.TryCast<HRGameManager>() : UnityEngine.Object.FindObjectOfType<HRGameManager>();

    public static HRGameInstance GameInstance =>
        BaseGameInstance.Get != null ? BaseGameInstance.Get.TryCast<HRGameInstance>() : UnityEngine.Object.FindObjectOfType<HRGameInstance>();

    public static HRPlayerController LocalPlayer =>
        NetworkClient.localPlayer != null ? NetworkClient.localPlayer.GetComponent<HRPlayerController>() : null;

    public static HeroPlayerCharacter LocalPawn =>
        LocalPlayer?.PlayerPawn != null ? LocalPlayer.PlayerPawn.TryCast<HeroPlayerCharacter>() : null;

    public static bool IsInGameplay
    {
        get
        {
            try
            {
                if (!NetworkClient.active || NetworkClient.localPlayer == null)
                    return false;

                string sceneName = SceneManager.GetActiveScene().name;
                if (!string.IsNullOrEmpty(sceneName) &&
                    (sceneName.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     sceneName.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     sceneName.IndexOf("Lobby", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     sceneName.IndexOf("Boot", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return false;
                }

                var pawn = LocalPawn;
                return pawn != null && pawn.gameObject != null && pawn.gameObject.activeInHierarchy;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public static HRWallet LocalWallet =>
        LocalPlayer?.Wallet;

    public static Vector3 PlayerHomeAnchor()
    {
        var pc = LocalPlayer;
        if (pc != null && pc.BedRespawnPoint != Vector3.zero)
            return pc.BedRespawnPoint;

        var pawn = LocalPawn;
        if (pawn != null)
            return pawn.transform.position;

        return Vector3.zero;
    }

    public static void Notify(string text, string hexColor)
    {
        try
        {
            var ui = GameInstance?.SLinkNotificationUI;
            if (ui == null) return;
            ColorUtility.TryParseHtmlString(hexColor, out var c);
            ui.ShowNotification(text, c);
        }
        catch (Exception) { }
    }

    public static void NotifyInfo(string text) => Notify(text, "#FFFFFF");
    public static void NotifyWarn(string text) => Notify(text, "#FFCC55");
    public static void NotifyError(string text) => Notify(text, "#FF5555");
    public static void NotifySuccess(string text) => Notify(text, "#55FF88");
}