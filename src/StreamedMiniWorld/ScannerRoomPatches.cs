using System.Runtime.CompilerServices;
using HarmonyLib;

namespace TerrainPatcher.StreamedMiniWorld;

internal static class ScannerRoomPatches {
    internal static MapRoomFunctionality? GetScannerRoom(MiniWorld miniWorld)
        => MINI_WORLD_MAP_ROOMS.TryGetValue(miniWorld, out var scannerRoom) ? scannerRoom : null;

    private static readonly ConditionalWeakTable<MiniWorld, MapRoomFunctionality>
        MINI_WORLD_MAP_ROOMS = new();

    [HarmonyPatch(typeof(MapRoomFunctionality), nameof(MapRoomFunctionality.Start))]
    private static class AddOnStart {
        private static void Prefix(MapRoomFunctionality __instance) => RegisterMapRoom(__instance);
    }

    private static void RegisterMapRoom(MapRoomFunctionality __instance) {
        if (MINI_WORLD_MAP_ROOMS.TryGetValue(__instance.miniWorld, out var cached)) {
            if (ReferenceEquals(__instance, cached)) return;

            MINI_WORLD_MAP_ROOMS.Remove(__instance.miniWorld);
            MINI_WORLD_MAP_ROOMS.Add(__instance.miniWorld, __instance);
        } else {
            MINI_WORLD_MAP_ROOMS.Add(__instance.miniWorld, __instance);
        }
    }
}
