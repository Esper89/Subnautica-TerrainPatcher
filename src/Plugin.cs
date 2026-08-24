using System.Collections;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace TerrainPatcher;

[BepInPlugin("Esper89.TerrainPatcher", "Terrain Patcher", "1.2.5")]
[BepInProcess("Subnautica.exe")]
[BepInProcess("SubnauticaZero.exe")]
internal sealed class Plugin : BaseUnityPlugin {
    private static Plugin instance;
    private static ManualLogSource logger;
    
    private void Awake() {
        instance = this;
        logger = base.Logger;
        LogDebug("Initializing Terrain Patcher");

        LogDebug("Applying Harmony patches");
        new Harmony("Esper89.TerrainPatcher").PatchAll();

        LogDebug("Finding and loading terrain patches");
        FileLoading.FindAndLoadPatches();

        LogDebug("Terrain Patcher initialized");
        
        StartCoroutine(DisplayQueuedErrorMessagesOnLoad());
    }

    internal static void LogDebug(string message) => logger.LogDebug(message);
    internal static void LogInfo(string message) => logger.LogInfo(message);
    internal static void LogWarning(string message) => logger.LogWarning(message);
    internal static void LogError(string message) => logger.LogError(message);
    internal static void LogFatal(string message) => logger.LogFatal(message);

    private static readonly List<string> QueuedMessages = new();
    // display an error message to the player once the title screen has loaded
    internal static void DisplayError(string message) {
        if(ErrorMessage.main == null) QueuedMessages.Add(message);
        else ErrorMessage.AddError(message);
    }

    private static IEnumerator DisplayQueuedErrorMessagesOnLoad() {
        yield return new WaitUntil(() => ErrorMessage.main != null);
        if (QueuedMessages.Count <= 0) yield break;
        foreach (string? message in QueuedMessages) { ErrorMessage.AddError($"[<#F00>ERROR</color>] {message}"); }
        QueuedMessages.Clear();
    }
    
    internal static string AssemblyDir => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
}
