using System;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Saleblazers.ModBase;

/// <summary>Shared access helpers for the game's managers, player and notifications.</summary>
internal static class ModService
{
    public static HRGameManager GameManager =>
        BaseGameManager.Get != null ? BaseGameManager.Get.TryCast<HRGameManager>() : UnityEngine.Object.FindObjectOfType<HRGameManager>();

    public static HRGameInstance GameInstance =>
        BaseGameInstance.Get != null ? BaseGameInstance.Get.TryCast<HRGameInstance>() : UnityEngine.Object.FindObjectOfType<HRGameInstance>();

    /// <summary>Local player controller (Mirror NetworkClient.localPlayer).</summary>
    public static HRPlayerController LocalPlayer =>
        NetworkClient.localPlayer != null ? NetworkClient.localPlayer.GetComponent<HRPlayerController>() : null;

    /// <summary>Local player's pawn (HeroPlayerCharacter), if possessed.</summary>
    public static HeroPlayerCharacter LocalPawn =>
        LocalPlayer?.PlayerPawn != null ? LocalPlayer.PlayerPawn.TryCast<HeroPlayerCharacter>() : null;

    /// <summary>True only when the player is actively spawned inside a game world (false in Main Menu / loading screens).</summary>
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

    /// <summary>Default anchor for deliveries/raids: the player's bed respawn point or their feet.</summary>
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
        catch (Exception)
        {
            // notifications are cosmetic
        }
    }

    public static void NotifyInfo(string text) => Notify(text, "#FFFFFF");
    public static void NotifyWarn(string text) => Notify(text, "#FFCC55");
    public static void NotifyError(string text) => Notify(text, "#FF5555");
    public static void NotifySuccess(string text) => Notify(text, "#55FF88");
}