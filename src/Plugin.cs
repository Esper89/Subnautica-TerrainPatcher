using System.Collections;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Bindings;

namespace TerrainPatcher;

[BepInPlugin("Esper89.TerrainPatcher", "Terrain Patcher", "1.3.0")]
[BepInDependency("com.snmodding.nautilus", BepInDependency.DependencyFlags.SoftDependency)]
[BepInProcess("Subnautica.exe")]
[BepInProcess("SubnauticaZero.exe")]
internal sealed class Plugin : BaseUnityPlugin {
    private static ManualLogSource? LOGGER = null;

    private void Awake() {
        LOGGER = Logger;
        LogDebug("Initializing Terrain Patcher");

        LogDebug("Applying Harmony patches");
        ApplyHarmonyPatches();

        LogDebug("Dispatching patcher thread");
        MainThreadDispatcher.Start(this);
        TerrainPatching.PatchingThread.BeginPatching();
        StartCoroutine(DisplayQueuedErrorMessages());
        LogDebug("Terrain Patcher initialized");

        if (Chainloader.PluginInfos.ContainsKey("com.snmodding.nautilus")) {
            TerrainPatching.PatchingThread.RegisterNautilusWaitScreen();
        }
    }

    private static void ApplyHarmonyPatches() {
        Harmony harmony = new("Esper89.TerrainPatcher");
        foreach (var ty in AccessTools.GetTypesFromAssembly(Assembly.GetExecutingAssembly())) {
            try {
                harmony.CreateClassProcessor(ty).Patch();
            } catch (Exception ex) {
                LogError($"Failed to apply Harmony patch {ty.FullName} with: {ex}");
            }
        }
    }

    internal static void LogDebug(string message) => LOGGER?.LogDebug(message);
    internal static void LogInfo(string message) => LOGGER?.LogInfo(message);
    internal static void LogWarning(string message) => LOGGER?.LogWarning(message);
    internal static void LogError(string message) => LOGGER?.LogError(message);
    internal static void LogFatal(string message) => LOGGER?.LogFatal(message);

    private static readonly List<string> QUEUED_MESSAGES = new();

    [ThreadSafe]
    internal static void DisplayError(string message) {
        MainThreadDispatcher.EnsureOnMainThread(() => {
            if (ErrorMessage.main == null) QUEUED_MESSAGES.Add(message);
            else DisplayErrorInGame(message);
        });
    }

    private static IEnumerator DisplayQueuedErrorMessages() {
        yield return new WaitUntil(() => ErrorMessage.main != null);
        if (QUEUED_MESSAGES.Count == 0) yield break;
        QUEUED_MESSAGES.ForEach(DisplayErrorInGame);
        QUEUED_MESSAGES.Clear();
    }

    private static void DisplayErrorInGame(string message)
        => ErrorMessage.AddError($"[<#F00>ERROR</color>] {message}");

    internal static string AssemblyDir = Path.GetDirectoryName(
        Assembly.GetExecutingAssembly().Location
    )!;
}

[BepInPlugin("TerrainExtender", "Terrain Extender (Provided by Terrain Patcher)", "2.0.0")]
[BepInDependency("Esper89.TerrainPatcher")]
[BepInProcess("Subnautica.exe")]
[BepInProcess("SubnauticaZero.exe")]
internal sealed class TerrainExtenderPlugin : BaseUnityPlugin { }
