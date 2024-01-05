using System;
using System.IO;
using System.Reflection;
using BepInEx;
using UnityEngine;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace TerrainPatcher
{
    [BepInPlugin("Esper89.TerrainPatcher", "Terrain Patcher", "1.0.2")]
    [BepInDependency("com.snmodding.nautilus", BepInDependency.DependencyFlags.SoftDependency)]
    internal class Mod : BaseUnityPlugin
    {
        // Initializes the mod.
        private void Awake()
        {
            this._settings = new Settings(base.Config);
            Mod.Instance = this;

            Patches.Register();
            FileLoading.FindAndLoadPatches();

            if (Chainloader.PluginInfos.ContainsKey("com.snmodding.nautilus"))
            {
                Options.Register();
            }
            else
            {
                Mod.LogInfo("Nautilus is not installed, no options menu will be available.");
            }
        }

        private static Mod? _instance;
        private static Mod Instance
        {
            get => Mod._instance ?? throw new InvalidOperationException(
                "Cannot call TerrainPatcher functions before it is loaded."
            );
            set => Mod._instance = value;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9))
            {
                MoveWorld.Move(new Vector3(100.0f, 0.0f, 100.0f));
            }
        }

        internal static void LogInfo(string message) => Mod.Instance.Logger.LogInfo(message);
        internal static void LogWarning(string message) => Mod.Instance.Logger.LogWarning(message);
        internal static void LogError(string message) => Mod.Instance.Logger.LogError(message);

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

        // All valid file extensions for the patch format.
        internal static readonly string[] PATCH_EXTENSIONS =
        {
            "optoctreepatch",
            "optoctreepatc", // Discord shortens uploaded file extensions to 13 characters.
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
}
