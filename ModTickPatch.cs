using System;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Saleblazers.ModBase;

/// <summary>
/// Per-frame hook driven from ModHost.Update (with HRDayManager.LateUpdate fallback).
/// Deduplicated by Time.frameCount so input is never processed twice in the same frame.
/// </summary>
internal static class ModTickPatch
{
    private static ManualLogSource _log;
    private static bool _firedOnce;
    private static int _lastTickFrame = -1;

    public static bool Apply(Harmony harmony, ManualLogSource log)
    {
        _log = log;
        try
        {
            var method = AccessTools.Method(typeof(HRDayManager), "LateUpdate", Type.EmptyTypes);
            if (method == null)
            {
                log.LogWarning("[JEI] HRDayManager.LateUpdate not found; relying on ModHost.");
                return false;
            }
            harmony.Patch(method, postfix: new HarmonyMethod(typeof(ModTickPatch), nameof(OnLateUpdate)));
            log.LogInfo("[JEI] Per-frame tick hooked (HRDayManager.LateUpdate).");
            return true;
        }
        catch (Exception e)
        {
            log.LogWarning($"[JEI] Per-frame hook failed: {e.Message}");
            return false;
        }
    }

    public static void TickFrame(HRDayManager dayManager = null)
    {
        try
        {
            int frame = Time.frameCount;
            if (frame != _lastTickFrame)
            {
                _lastTickFrame = frame;
                if (!_firedOnce)
                {
                    _firedOnce = true;
                    _log?.LogInfo("[JEI] Per-frame tick FIRING.");
                }
                JeiUI.HandleInput();
            }
        }
        catch (Exception e)
        {
            _log?.LogError($"[JEI] Tick error: {e}");
        }
    }

    public static void OnLateUpdate(HRDayManager __instance)
    {
        TickFrame(__instance);
    }
}
