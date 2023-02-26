

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using DefaultNamespace;
using HarmonyLib;
using SMLHelper.V2.Utility;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.PostProcessing;
using UnityEngine.SceneManagement;
using UWE;
using WorldStreaming;

namespace TerrainPatcher
{
    // Harmony patches that make the game open patched terrain files instead of the originals.
    internal static class Patches
    {
        [HarmonyPatch(
            typeof(LargeWorldStreamer),
            nameof(LargeWorldStreamer.GetCompiledOctreesCachePath)
        )]
        internal static class LargeWorldStreamer_GetCompiledOctreesCachePath_Patch
        {
            // Matches a batch file name. Supports negative batch numbers.
            private static readonly Regex pattern = new Regex(
                $@"^(?:.*(?:\/|\\))?compiled-batch-(-?\d+)-(-?\d+)-(-?\d+)\.optoctrees$"
            );

            private static bool Prefix(string filename, ref string __result, bool __runOriginal)
            {
                // We need to parse the filename into a batch ID before we can run SetResult.

                var match = pattern.Match(filename);
                Int3 batchId;

                try
                {
                    batchId = new Int3(
                        int.Parse(match.Groups[1].Value),
                        int.Parse(match.Groups[2].Value),
                        int.Parse(match.Groups[3].Value)
                    );
                }
                catch (FormatException)
                {
                    Mod.LogError($"Game accessed batch file with invalid filename '{filename}'");
                    return false;
                }

                return SetResult(batchId, ref __result, __runOriginal);
            }
        }

        [HarmonyPatch(typeof(BatchOctreesStreamer), nameof(BatchOctreesStreamer.GetPath))]
        internal static class BatchOctreesStreamer_GetPath_Patch
        {
            private static bool Prefix(Int3 batchId, ref string __result, bool __runOriginal)
                => SetResult(batchId, ref __result, __runOriginal);
        }

        // Sets the result of the method and skips the original if needed.
        private static bool SetResult(Int3 batchId, ref string result, bool runOriginal)
        {
            if (TerrainRegistry.patchedBatches.ContainsKey(batchId) && runOriginal)
            {
                result = TerrainRegistry.patchedBatches[batchId];
                return false;
            }
            else
            {
                return true;
            }
        }

        [HarmonyPatch(typeof(Player))]
        internal static class Player_SetPosition_Patch
        {
            [HarmonyPatch(nameof(Player.SetPosition), new Type[] { typeof(Vector3) })]
            [HarmonyPrefix]
            private static void Prefix(ref Vector3 wsPos)
            {
                // TODO only works when very far from 0,0,0. fix
                var trace = new StackTrace();
                Mod.LogInfo($"called by {trace.GetFrame(2).GetMethod().Name}");
                if (trace.GetFrame(2).GetMethod().Name.Contains(nameof(Player.SpawnNearby)))
                    return;
                wsPos -= twoloop.OriginShift.LocalOffset.ToVector3();
            }

            [HarmonyPatch(nameof(Player.Awake))]
            [HarmonyPostfix]
            private static void Postfix(Player __instance)
            {
                var shift = __instance.gameObject.AddComponent<twoloop.OriginShift>();
                shift.focus = __instance.gameObject.transform;
            }
        }

/*
        [HarmonyPatch(typeof(LargeWorldEntity))]
        internal static class LargeWorldEntity_Patch
        {
            [HarmonyPatch("OnDe")]
            [HarmonyPrefix]
            private static void Prefix(LargeWorldEntity __instance)
            {
                __instance.transform.position += twoloop.OriginShift.LocalOffset.ToVector3();
            }
        }
        */
        [HarmonyPatch(typeof(UniqueIdentifier))]
        internal static class UniqueIdentifier_Patch
        {
            [HarmonyPatch(nameof(UniqueIdentifier.Awake))]
            [HarmonyPostfix]
            private static void Postfix(UniqueIdentifier __instance)
            {
                if (__instance is ChildObjectIdentifier)
                    return;
                if (__instance.transform.parent != null || __instance.gameObject.GetComponent<Crash>())
                    return;
                __instance.transform.position -= twoloop.OriginShift.LocalOffset.ToVector3();
                __instance.gameObject.AddComponent<tracker>();
            }
        }

        [HarmonyPatch(typeof(BreakableResource))]
        internal static class BreakableResource_Patch
        {
            [HarmonyPatch(nameof(BreakableResource.SpawnResourceFromPrefab),
                new[] { typeof(AssetReferenceGameObject), typeof(Vector3), typeof(Vector3) })]
            [HarmonyPrefix]
            private static void Prefix(ref Vector3 position)
            {
                position += twoloop.OriginShift.LocalOffset.ToVector3();
            }
        }

        [HarmonyPatch(typeof(SignalPing))]
        internal static class SignalPing_Patch
        {
            [HarmonyPatch(nameof(SignalPing.Start))]
            [HarmonyPrefix]
            private static void Prefix(SignalPing __instance)
            {
                __instance.pos -= twoloop.OriginShift.LocalOffset.ToVector3();
            }
        }
    }

    [HarmonyPatch(typeof(SpawnOnKill))]
    internal static class SpawnOnKill_Patch
    {
        [HarmonyPatch(nameof(SpawnOnKill.OnKill))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var _instructions = new List<CodeInstruction>(instructions);
            for (var i = 0; i < _instructions.Count; i++)
            {
                if (_instructions[i].Calls(typeof(Transform).GetProperty(nameof(Transform.position))?.GetGetMethod()))
                {
                    _instructions[i] = Transpilers.EmitDelegate<Func<Transform, Vector3>>(transform =>
                    {
                        return transform.position + twoloop.OriginShift.LocalOffset.ToVector3();
                    });
                }
            }

            return _instructions.AsEnumerable();
        }
    }

    [HarmonyPatch(typeof(IngameMenu))]
    internal static class IngameMenu_Patch
    {
        [HarmonyPatch(nameof(IngameMenu.SaveGame))]
        [HarmonyPrefix]
        private static void Prefix()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var rootGameObjects = SceneManager.GetSceneAt(i).GetRootGameObjects();
                foreach (var item in rootGameObjects)
                {
                    if(item.GetComponent<tracker>() && !item.GetComponent<Player>())
                        item.GetComponent<tracker>().GetReadyForSave();
                }
            }
        }

        [HarmonyPatch(nameof(IngameMenu.SaveGame))]
        [HarmonyPostfix]
        private static void Postfix()
        {
            CoroutineHost.StartCoroutine(SaveGame_Postfix_Enumerator());
        }

        private static IEnumerator SaveGame_Postfix_Enumerator()
        {
            yield return WaitFor.Frames(75);
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var rootGameObjects = SceneManager.GetSceneAt(i).GetRootGameObjects();
                foreach (var item in rootGameObjects)
                {
                    if(item.GetComponent<tracker>() && !item.GetComponent<Player>())
                        item.GetComponent<tracker>().StopSave();
                }
            }
        }

    }

    [HarmonyPatch(typeof(ReefbackPlant))]
    internal static class ReefbackPlant_Patch
    {
        [HarmonyPatch(nameof(ReefbackPlant.Start))]
        [HarmonyPrefix]
        private static void Prefix(ReefbackPlant __instance)
        {
            __instance.transform.position += twoloop.OriginShift.LocalOffset.ToVector3();
        }
    }
}

/*
        [HarmonyPatch(typeof(ConstructionObstacle))]
        internal static class ConstructionObstacle_Patch
        {
            [HarmonyPatch("Awake")]
            [HarmonyPrefix]
            private static bool Prefix(ConstructionObstacle __instance)
            {
                if (__instance.name.ToLower().Contains("nonstreaming"))
                {
                    __instance.gameObject.AddComponent<constructionobstacletracker>();
                }

                return false;
            }

        }
        */
/*
[HarmonyPatch(typeof(Transform), nameof(Transform.position), MethodType.Getter)]
internal static class Transform_position_get_Patch
{
    private static void Postfix(ref Vector3 __result, Transform __instance)
    {
        if (__instance.gameObject.name.ToLower() == "maincamera")
        {
            __result -= twoloop.OriginShift.LocalOffset.ToVector3();
        }
    }
}
[HarmonyPatch(typeof(Transform), nameof(Transform.position), MethodType.Setter)]
internal static class Transform_position_set_Patch
{
    private static void Prefix(ref Vector3 value, Transform __instance)
    {
        if (__instance != MoveWorld.GLOBAL_ROOT!)
        {
            value += MoveWorld.GLOBAL_ROOT!.position;
        }
    }
    */