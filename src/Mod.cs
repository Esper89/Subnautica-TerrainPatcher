using System.Reflection;
using BepInEx;
using HarmonyLib;

namespace TerrainPatcher;

[BepInPlugin("Esper89.TerrainPatcher", "Terrain Patcher", "1.2.4")]
[BepInProcess("Subnautica.exe")]
[BepInProcess("SubnauticaZero.exe")]
sealed class Mod : BaseUnityPlugin {
    void Awake() {
        Mod.Instance = this;
        Mod.LogDebug("Initializing Terrain Patcher");

        Mod.LogDebug("Applying Harmony patches");
        new Harmony("Esper89.TerrainPatcher").PatchAll();

        Mod.LogDebug("Finding and loading terrain patches");
        FileLoading.FindAndLoadPatches();

        Mod.LogDebug("Terrain Patcher initialized");
    }

    void Update() {
        if (this.messages.Count > 0 && ErrorMessage.main != null) {
            foreach (var message in this.messages) { ErrorMessage.AddError(message); }
            this.messages.Clear();
        }
    }

    static Mod? instance;
    internal static Mod Instance {
        get => Mod.instance ?? throw new InvalidOperationException(
            "cannot call Terrain Patcher functions before it is loaded"
        );
        set => Mod.instance = value;
    }

    internal static void LogDebug(string message) => Mod.Instance.Logger.LogDebug(message);
    internal static void LogInfo(string message) => Mod.Instance.Logger.LogInfo(message);
    internal static void LogWarning(string message) => Mod.Instance.Logger.LogWarning(message);
    internal static void LogError(string message) => Mod.Instance.Logger.LogError(message);
    internal static void LogFatal(string message) => Mod.Instance.Logger.LogFatal(message);

    // display an error message to the player once the title screen has loaded
    internal static void DisplayError(string message) => Mod.Instance.messages.Add(message);
    List<string> messages = new();

    internal static string AssemblyDir
        => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
}

[BepInPlugin(
    "TerrainExtender",
    "Terrain Extender [Provided by Terrain Patcher]",
    "2.0.0"
)]
[BepInDependency("Esper89.TerrainPatcher")]
sealed class TerrainExtender : BaseUnityPlugin { }
