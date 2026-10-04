using System;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Saleblazers.ModBase;

/// <summary>
/// Prevents Saleblazers from hiding/locking the mouse cursor or rotating the camera / swinging weapons
/// while JEI is open in gameplay.
/// Never touches the cursor in the Main Menu.
/// </summary>
internal static class CursorPatch
{
    private static ManualLogSource _log;
    private static bool _wasOpen;
    private static bool _preOpenCursorVisible;
    private static CursorLockMode _preOpenLockMode = CursorLockMode.Locked;

    public static bool IsModUiOpen => ModService.IsInGameplay && JeiUI.IsVisible;

    public static void Apply(Harmony harmony, ManualLogSource log)
    {
        _log = log;
        try
        {
            var giType = typeof(HRGameInstance).BaseType; // BaseGameInstance
            if (giType != null)
            {
                var reqMouse = AccessTools.Method(giType, "RequestMouseState", new[] { typeof(bool), typeof(CursorLockMode) });
                if (reqMouse != null)
                    harmony.Patch(reqMouse, postfix: new HarmonyMethod(typeof(CursorPatch), nameof(OnMouseStatePostfix)));

                var setVis = AccessTools.Method(giType, "SetCursorVisibility", new[] { typeof(bool) });
                if (setVis != null)
                    harmony.Patch(setVis, postfix: new HarmonyMethod(typeof(CursorPatch), nameof(OnMouseStatePostfix)));

                var resetMouse = AccessTools.Method(giType, "ResetToRequestedMouseState", Type.EmptyTypes);
                if (resetMouse != null)
                    harmony.Patch(resetMouse, postfix: new HarmonyMethod(typeof(CursorPatch), nameof(OnMouseStatePostfix)));
            }

            var pawnType = typeof(HeroPlayerCharacter);
            var lookX = AccessTools.Method(pawnType, "HandleMouseLookX", new[] { typeof(float) });
            if (lookX != null)
                harmony.Patch(lookX, prefix: new HarmonyMethod(typeof(CursorPatch), nameof(BlockWhenUiOpen)));

            var lookY = AccessTools.Method(pawnType, "HandleMouseLookY", new[] { typeof(float) });
            if (lookY != null)
                harmony.Patch(lookY, prefix: new HarmonyMethod(typeof(CursorPatch), nameof(BlockWhenUiOpen)));

            var primaryMouse = AccessTools.Method(pawnType, "PrimaryMouseEvent", new[] { typeof(bool) });
            if (primaryMouse != null)
                harmony.Patch(primaryMouse, prefix: new HarmonyMethod(typeof(CursorPatch), nameof(BlockWhenUiOpen)));

            log.LogInfo("[JEI] Cursor & camera lock patches applied.");
        }
        catch (Exception e)
        {
            log.LogWarning($"[JEI] CursorPatch failed: {e.Message}");
        }
    }

    /// <summary>Synchronizes the game's internal cursor manager when JEI opens or closes in gameplay.</summary>
    public static void SyncCursorState()
    {
        try
        {
            if (!ModService.IsInGameplay)
            {
                _wasOpen = false;
                return;
            }

            bool open = JeiUI.IsVisible;
            if (open && !_wasOpen)
            {
                _preOpenCursorVisible = Cursor.visible;
                _preOpenLockMode = Cursor.lockState;
            }
            _wasOpen = open;

            var pawn = ModService.LocalPawn;
            if (pawn != null)
            {
                pawn.SuspendPrimaryMouseGameplay(open);
            }

            if (open)
            {
                BaseGameInstance.SetCursorVisibility(true);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                BaseGameInstance.ResetToRequestedMouseState();
                Cursor.lockState = _preOpenLockMode;
                Cursor.visible = _preOpenCursorVisible;
            }
        }
        catch (Exception) { }
    }

    public static void EnforceCursorEachFrame()
    {
        if (!IsModUiOpen) return;
        try
        {
            if (Cursor.lockState != CursorLockMode.None)
                Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible)
                Cursor.visible = true;
        }
        catch (Exception) { }
    }

    private static void OnMouseStatePostfix()
    {
        if (IsModUiOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private static bool BlockWhenUiOpen()
    {
        return !IsModUiOpen;
    }
}
