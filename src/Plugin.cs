using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Nautilus.Handlers;
using UnityEngine;
using UnityEngine.Bindings;

namespace TerrainPatcher;

[BepInPlugin("Esper89.TerrainPatcher", PLUGIN_NAME, "1.2.5")]
[BepInProcess("Subnautica.exe")]
[BepInProcess("SubnauticaZero.exe")]
internal sealed class Plugin : BaseUnityPlugin {
    private const string PLUGIN_NAME = "Terrain Patcher";

    internal static Plugin instance = null!;
    private static ManualLogSource logger = null!;

    private void Awake() {
        instance = this;
        logger = Logger;
        LogDebug("Initializing Terrain Patcher");

        LogDebug("Applying Harmony patches");
        new Harmony("Esper89.TerrainPatcher").PatchAll();

        LogDebug("Dispatching patcher thread");
        UnityThreadDispatcher.Start(this);
        PatchThreading.BeginPatching();
        StartCoroutine(DisplayQueuedErrorMessages());
        LogDebug("Terrain Patcher initialized");

        WaitScreenHandler.RegisterAsyncLoadTask(
            PLUGIN_NAME, PatchThreading.EnsurePatchingFinished, "Patching Terrain"
        );
    }

    internal static void LogDebug(string message) => logger.LogDebug(message);
    internal static void LogInfo(string message) => logger.LogInfo(message);
    internal static void LogWarning(string message) => logger.LogWarning(message);
    internal static void LogError(string message) => logger.LogError(message);
    internal static void LogFatal(string message) => logger.LogFatal(message);

    private static readonly List<string> QueuedMessages = new();

    [ThreadSafe]
    internal static void DisplayError(string message) {
        UnityThreadDispatcher.EnsureOnUnityThread(() => {
            if (ErrorMessage.main == null) QueuedMessages.Add(message);
            else DisplayErrorInGame(message);
        });
    }

    private static IEnumerator DisplayQueuedErrorMessages() {
        yield return new WaitUntil(() => ErrorMessage.main != null);
        if (QueuedMessages.Count == 0) yield break;
        QueuedMessages.ForEach(DisplayErrorInGame);
        QueuedMessages.Clear();
    }

    private static void DisplayErrorInGame(string message)
        => ErrorMessage.AddError($"[<#F00>ERROR</color>] {message}");

    internal static string AssemblyDir
        => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
}
