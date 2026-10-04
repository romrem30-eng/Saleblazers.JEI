using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace Saleblazers.ModBase;

[BepInPlugin("com.saleblazers.jei", "Saleblazers JEI", "1.0.0")]
public class Plugin : BasePlugin
{
    private ManualLogSource _logger;
    private Harmony _harmony;
    private GameObject _hostObject;

    public override void Load()
    {
        _logger = Log;

        _logger.LogInfo("Saleblazers JEI v1.0.0 initialized successfully");
        _logger.LogInfo($"Unity version: {Application.unityVersion}");

        JeiCatalog.Init(_logger);
        JeiUI.Init(Config, _logger);

        // Register custom MonoBehaviour in IL2CPP runtime for guaranteed per-frame ticks
        try
        {
            ClassInjector.RegisterTypeInIl2Cpp<ModHost>();
            _hostObject = new GameObject("SaleblazersJeiHost");
            UnityEngine.Object.DontDestroyOnLoad(_hostObject);
            _hostObject.hideFlags = HideFlags.HideAndDontSave;
            _hostObject.AddComponent<ModHost>();
            ModHost.SetLogger(_logger);
            _logger.LogInfo("ModHost MonoBehaviour registered and active.");
        }
        catch (Exception e)
        {
            _logger.LogWarning($"Failed to register ModHost MonoBehaviour: {e.Message}");
        }

        _harmony = new Harmony("com.saleblazers.jei");
        _harmony.PatchAll();
        ModTickPatch.Apply(_harmony, _logger);
        CursorPatch.Apply(_harmony, _logger);
        _logger.LogInfo("Harmony patches applied");
    }

    public override bool Unload()
    {
        _harmony?.UnpatchSelf();
        if (_hostObject != null)
        {
            UnityEngine.Object.Destroy(_hostObject);
        }
        return true;
    }
}
