using System.Reflection;
using BepInEx;
using HarmonyLib;

namespace TerrainPatcher;

[BepInPlugin("Esper89.TerrainPatcher", "Terrain Patcher", "1.2.5")]
[BepInProcess("Subnautica.exe")]
[BepInProcess("SubnauticaZero.exe")]
internal sealed class Plugin : BaseUnityPlugin {
    private static Plugin Instance;
    
    private void Awake() {
        Instance = this;
        LogDebug("Initializing Terrain Patcher");

        LogDebug("Applying Harmony patches");
        new Harmony("Esper89.TerrainPatcher").PatchAll();

        LogDebug("Finding and loading terrain patches");
        FileLoading.FindAndLoadPatches();

        LogDebug("Terrain Patcher initialized");
    }
    
    //TODO: I dont like this constant polling. ya its probably negligible but why do it when we dont have to . We should have a better system in place
    private void Update() {
        if (messages.Count > 0 && ErrorMessage.main != null) {
            foreach (var message in this.messages) { ErrorMessage.AddError(message); }
            messages.Clear();
        }
    }

    internal static void LogDebug(string message) => Instance.Logger.LogDebug(message);
    internal static void LogInfo(string message) => Instance.Logger.LogInfo(message);
    internal static void LogWarning(string message) => Instance.Logger.LogWarning(message);
    internal static void LogError(string message) => Instance.Logger.LogError(message);
    internal static void LogFatal(string message) => Instance.Logger.LogFatal(message);

    // display an error message to the player once the title screen has loaded
    internal static void DisplayError(string message) => Instance.messages.Add(message);
    private List<string> messages = new();

    internal static string AssemblyDir => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
}
