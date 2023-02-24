

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.Linq;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using DefaultNamespace;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AddressableAssets;
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
            private static bool Prefix(ref Vector3 wsPos)
            {
                // TODO only works when very far from 0,0,0. fix
                wsPos -= twoloop.OriginShift.LocalOffset.ToVector3();
                return true;
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
            [HarmonyPrefix]
            private static void Prefix(UniqueIdentifier __instance)
            {
                if (__instance is ChildObjectIdentifier)
                    return;
                if (__instance.transform.parent != null)
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

        [HarmonyPatch(typeof(SeaTreader))]
        internal static class SeaTreader_Patch
        {
            [HarmonyPatch(nameof(SeaTreader.FindClosestPathPoint))]
            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> _instructions = new(instructions);
                for (var i = 0; i < _instructions.Count; i++)
                {
                    if (_instructions[i].LoadsField(typeof(SeaTreader).GetField(nameof(SeaTreader.treaderPaths))))
                    {
                        _instructions[i] = Transpilers.EmitDelegate<Func<SeaTreader, TreaderPath[]>>(treader =>
                        {
                            var toreturn = new List<TreaderPath>(treader.treaderPaths);
                            foreach (var path in toreturn)
                            {
                                foreach (var point in path.pathPoints)
                                {
                                    point.position -= twoloop.OriginShift.LocalOffset.ToVector3();
                                }
                            }

                            return toreturn.ToArray();
                        });
                    }
                }

                return _instructions.AsEnumerable();
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