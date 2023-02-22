

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;
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
            private static bool Prefix(ref Vector3 wsPos)
            {
                // TODO only works when very far from 0,0,0. fix
                wsPos -= twoloop.OriginShift.LocalOffset.ToVector3();
                return false;
            }
            [HarmonyPatch(nameof(Player.Awake))]
            [HarmonyPostfix]
            private static void Postfix(Player __instance)
            {
                __instance.gameObject.AddComponent<twoloop.OriginShift>().focus = __instance.gameObject.transform;
            }
        }
        /*
        [HarmonyPatch(typeof(LargeWorldEntity))]
        internal static class LargeWorldEntity_Patch
        {
            [HarmonyPatch(nameof(LargeWorldEntity.Awake))]
            [HarmonyPrefix]
            private static void Prefix(LargeWorldEntity __instance)
            {
                __instance.transform.position -= twoloop.OriginShift.LocalOffset.ToVector3();
            }
        }
        [HarmonyPatch(typeof(LargeWorldStreamer))]
        internal static class LargeWorldStreamer_Patch
        {
            [HarmonyPatch(nameof(LargeWorldStreamer.GetContainingBatch))]
            [HarmonyPrefix]
            private static void Prefix(ref Vector3 wsPos)
            {
                wsPos -= twoloop.OriginShift.LocalOffset.ToVector3();
            }

            [HarmonyPatch(nameof(LargeWorldStreamer.GetBlock))]
            [HarmonyPrefix]
            private static void Prefix2(ref Vector3 wsPos)
            {
                wsPos -= twoloop.OriginShift.LocalOffset.ToVector3();
            }
        }
        */
        [HarmonyPatch(typeof(UniqueIdentifier))]
        internal static class UniqueIdentifier_Patch
        {
            [HarmonyPatch(nameof(UniqueIdentifier.Awake))]
            [HarmonyPrefix]
            private static void Prefix(UniqueIdentifier __instance)
            {
                if (__instance is not PrefabIdentifier prefabIdentifier)
                    return;
                if (prefabIdentifier.transform.parent != null)
                    return;
                prefabIdentifier.transform.position -= twoloop.OriginShift.LocalOffset.ToVector3();
            }
        }
    }

    /*
    [HarmonyPatch(typeof(Transform), nameof(Transform.position), MethodType.Getter)]
    internal static class Transform_position_get_Patch
    {
        private static void Postfix(ref Vector3 __result, Transform __instance)
        {
            if (__instance != MoveWorld.GLOBAL_ROOT!)
            {
                __result -= MoveWorld.GLOBAL_ROOT!.position;
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
        }