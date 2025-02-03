using System.Collections.Generic;
using HarmonyLib;
using Nautilus.Utility;

namespace TerrainPatcher
{
    // Allows entities to save and exist on negative batches.
    [HarmonyPatch(typeof(Array3<>))]
    internal static class Array3Patches
    {
        internal static void Patch(Harmony harmony)
        {
            var type = typeof(Array3<>).MakeGenericType(typeof(EntityCell));
            var getMethod = AccessTools.Method(type, "Get");
            harmony.Patch(getMethod, prefix: new HarmonyMethod(
                AccessTools.Method(typeof(Array3Patches), nameof(GetPrefix))
            ));
            var setMethod = AccessTools.Method(type, "Set");
            harmony.Patch(setMethod, prefix: new HarmonyMethod(
                AccessTools.Method(typeof(Array3Patches), nameof(SetPrefix))
            ));

            SaveUtils.RegisterOnQuitEvent(() => { _vanillaToNegativeArrays.Clear(); });
        }

        private class NegativeEntityCell
        {
            public Dictionary<Int3, EntityCell> EntityCells { get; }

            public NegativeEntityCell(Dictionary<Int3, EntityCell> entityCells)
            {
                EntityCells = entityCells;
            }
        }

        private static Dictionary<Array3<EntityCell>, NegativeEntityCell> _vanillaToNegativeArrays =
            new Dictionary<Array3<EntityCell>, NegativeEntityCell>();

        private static bool GetPrefix(
            Array3<EntityCell> __instance,
            int x, int y, int z,
            ref EntityCell __result
        )
        {
            var index = __instance.GetIndex(x, y, z);
            if (index >= 0)
            {
                return true;
            }

            // At this point the index is negative, so we'll handle it ourselves.
            if (_vanillaToNegativeArrays.TryGetValue(__instance, out var negativesArray) &&
                negativesArray.EntityCells.TryGetValue(new Int3(x, y, z), out var entityCell))
            {
                __result = entityCell;
                Mod.LogDebug($"Get negative entity cell for ({x},{y},{z})");
                return false;
            }

            Mod.LogDebug($"Couldn't find negative entity cell for ({x},{y},{z})");
            return false;
        }

        private static bool SetPrefix(
            Array3<EntityCell> __instance,
            int x, int y, int z,
            EntityCell value
        )
        {
            var index = __instance.GetIndex(x, y, z);
            if (index >= 0)
            {
                return true;
            }

            // At this point the index is negative, so we'll set it to our collection.
            if (!_vanillaToNegativeArrays.TryGetValue(__instance, out var negativeEntityCell))
            {
                negativeEntityCell = new NegativeEntityCell(
                    new Dictionary<Int3, EntityCell>(Int3.equalityComparer)
                );
                _vanillaToNegativeArrays[__instance] = negativeEntityCell;
            }

            negativeEntityCell.EntityCells[new Int3(x, y, z)] = value;
            Mod.LogDebug($"Set negative entity cell for ({x},{y},{z})");

            return false;
        }
    }
}
