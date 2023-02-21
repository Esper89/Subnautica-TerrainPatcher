using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace TerrainPatcher
{
    [BepInPlugin("Esper89.TerrainPatcher", "Terrain Patcher", "1.0.0")]
    internal class Mod : BaseUnityPlugin
    {
        // Initializes the mod.
        private void Awake()
        {
            var harmony = new Harmony("Esper89/TerrainPatcher");
            LOGGER = this.Logger;
            harmony.PatchAll();
            FileLoading.FindAndLoadPatches();

            LogInfo("Done loading!");
        }

        private static ManualLogSource? LOGGER;

        

        internal static void LogInfo(string message) => LOGGER!.LogInfo(message);
        internal static void LogWarning(string message) => LOGGER!.LogWarning(message);
        internal static void LogError(string message) => LOGGER!.LogError(message);
    }

    internal static class Constants
    {
        // The current version number of the optoctreepatch format.
        internal const uint PATCH_VERSION = 0;

        // The current file extension for the optoctreepatch format.
        internal static readonly string PATCH_EXTENSION = ".optoctreepatch";

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
