using System;
using BepInEx.Logging;
using UnityEngine;

namespace Saleblazers.ModBase;

public class ModHost : MonoBehaviour
{
    private static ManualLogSource _log;

    public ModHost(IntPtr ptr) : base(ptr) { }

    public static void SetLogger(ManualLogSource log)
    {
        _log = log;
    }

    private void Update()
    {
        ModTickPatch.TickFrame();
    }

    private void LateUpdate()
    {
        CursorPatch.EnforceCursorEachFrame();
    }
}
