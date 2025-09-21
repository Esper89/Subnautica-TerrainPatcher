using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;

namespace TerrainPatcher
{
    [BepInPlugin("Esper89.TerrainPatcher", "Terrain Patcher", "1.2.3")]
    [BepInDependency("com.snmodding.nautilus", BepInDependency.DependencyFlags.SoftDependency)]
    internal class Mod : BaseUnityPlugin
    {
        // Initializes the mod.
        private void Awake()
        {
            this._settings = new Settings(base.Config);
            Mod.Instance = this;

            var harmony = new Harmony("Esper89.TerrainPatcher");
            BatchPatches.Patch(harmony);
            Array3Patches.Patch(harmony);
            WorldStreamerPatches.Patch(harmony);

            FileLoading.FindAndLoadPatches();

            if (Chainloader.PluginInfos.ContainsKey("com.snmodding.nautilus"))
            {
                Mod.LogInfo("Nautilus is installed, options menu will be available");
                Options.Register();
            }
            else
            {
                Mod.LogWarning("Nautilus is not installed, no options menu will be available");
            }
        }

        // Prints error messages.
        private void Update()
        {
            if (this.messages.Count > 0 && ErrorMessage.main != null)
            {
                foreach (string message in this.messages) { ErrorMessage.AddError(message); }
                this.messages.Clear();
            }
        }

        private static Mod? _instance;
        private static Mod Instance
        {
            get => Mod._instance ?? throw new InvalidOperationException(
                "Cannot call TerrainPatcher functions before it is loaded"
            );
            set => Mod._instance = value;
        }

        // Writes a message to the BepInEx log with the specified log level.
        internal static void LogDebug(string message) => Mod.Instance.Logger.LogDebug(message);
        internal static void LogInfo(string message) => Mod.Instance.Logger.LogInfo(message);
        internal static void LogWarning(string message) => Mod.Instance.Logger.LogWarning(message);
        internal static void LogError(string message) => Mod.Instance.Logger.LogError(message);

        // Displays an error message to the player once the title screen has loaded.
        internal static void DisplayError(string message) => Mod.Instance.messages.Add(message);
        private List<string> messages = new List<string>();

        private Settings? _settings;
        internal static Settings Settings { get => Mod.Instance._settings!; }
    }

    internal class Settings
    {
        internal Settings(ConfigFile config)
        {
            this.includePatches = config.Bind(
                "terrain", "include-patches", true, "Include patches when loading terrain?"
            );

            config.Save();
        }

        private ConfigEntry<bool> includePatches;

        internal bool IncludePatches
        {
            get => this.includePatches.Value;
            set => this.includePatches.Value = value;
        }
    }

    internal static class Constants
    {
        // The current version number of the patch format.
        internal const uint PATCH_VERSION = 0;

        // The patch format version number that's guaranteed to be skipped.
        internal const uint SKIP_VERSION = uint.MaxValue;

        // All valid file extensions for the patch format.
        internal static readonly string[] PATCH_EXTENSIONS =
        {
            "optoctreepatch",
            "optoctreepatc", // Discord used to shorten uploaded file extensions to 13 characters.
        };

        // The current version number of the game's batch format.
        internal const uint BATCH_VERSION = 4;

        // The number of octrees in a batch.
        internal const byte OCTREES_PER_BATCH = 125;

        // The mod's directory.
        internal static readonly string MOD_DIR =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        // Directories to search for vanilla batches.
        internal static readonly string[] ORIG_BATCH_DIRS =
        {
            "Build18",
            "Expansion",
        };
    }

    [BepInPlugin(
        "TerrainExtender",
        "Terrain Extender [Provided by Terrain Patcher]",
        "2.0.0"
    )]
	[BepInDependency("Esper89.TerrainPatcher")]
    internal class TerrainExtender : BaseUnityPlugin { }
}
