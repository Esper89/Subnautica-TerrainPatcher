using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.Remoting.Channels;
using BepInEx;
using BepInEx.Logging;
using DefaultNamespace;
using HarmonyLib;
using SMLHelper.V2.Handlers;
using SMLHelper.V2.Json;
using SMLHelper.V2.Json.Attributes;
using UnityEngine;
using UnityEngine.SceneManagement;
using UWE;

namespace TerrainPatcher
{
    [BepInPlugin("Esper89.TerrainPatcher", "Terrain Patcher", "1.0.0")]
    internal class Mod : BaseUnityPlugin
    {
        public static Vector3 oldoffset = Vector3.zero;

        public static bool isloading = false;
        // Initializes the mod.
        private void Awake()
        {
            var harmony = new Harmony("Esper89/TerrainPatcher");
            LOGGER = this.Logger;
            harmony.PatchAll();
            FileLoading.FindAndLoadPatches();
            var data = SaveDataHandler.Main.RegisterSaveDataCache<StorePos>();
            data.OnFinishedLoading += (object sender, JsonFileEventArgs e) =>
            {
                var thedata = e.Instance as StorePos;
                Player.main.transform.position += thedata.offsetpos;
                twoloop.OriginShift.Recenter();
                oldoffset = thedata.offsetpos;
            };
            data.OnStartedSaving += (object sender, JsonFileEventArgs e) =>
            {
                var tosave = e.Instance as StorePos;
                tosave.offsetpos = twoloop.OriginShift.LocalOffset.ToVector3();
            };
            LogInfo("Done loading!");
        }
        public void Update()
        {
            if (Input.GetKeyUp(KeyCode.F9))
            {
                twoloop.OriginShift.Recenter();
            }
        }
        private static ManualLogSource? LOGGER;

        

        internal static void LogInfo(string message) => LOGGER!.LogInfo(message);
        internal static void LogWarning(string message) => LOGGER!.LogWarning(message);
        internal static void LogError(string message) => LOGGER!.LogError(message);
    }
    [FileName("Terrain_Patcher_Save_Offset")]
    internal class StorePos : SaveDataCache
    {
            public Vector3 offsetpos { get; set; }
    }
    public static class WaitFor
    {
        public static IEnumerator Frames(int frameCount)
        {
            if (frameCount <= 0)
            {
                throw new ArgumentOutOfRangeException("frameCount", "Cannot wait for less that 1 frame");
            }

            while (frameCount > 0)
            {
                frameCount--;
                yield return null;
            }
        }
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
