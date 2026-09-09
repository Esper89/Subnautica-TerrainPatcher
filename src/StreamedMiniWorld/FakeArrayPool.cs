using HarmonyLib;
using Unity.Collections;
using UWE;

namespace TerrainPatcher.StreamedMiniWorld;

/// <summary>To not overuse the game's allocator for terrain, this is a simple duplicate that,
/// counter to the original purpose, allocates on demand instead of pooling. Performance seems to be
/// in line with pooling, given meshes are not requested very often and allocations happen off the
/// main thread.</summary>
internal sealed class FakeArrayPool() : SplitNativeArrayPool<byte>(0, 0, 0, 0, 0, 0, 0, 0) {
    [HarmonyPatch(typeof(SplitNativeArrayPool<byte>), nameof(Get))]
    private static class CustomArrayPoolGetImpl {
        private static bool Prefix(
            SplitNativeArrayPool<byte> __instance, int minLength, ref NativeArray<byte> __result
        ) {
            if (__instance is not FakeArrayPool) return true;
            __result = new(minLength, Allocator.Persistent);
            return false;
        }
    }

    [HarmonyPatch(typeof(SplitNativeArrayPool<byte>), nameof(Return))]
    private static class CustomArrayPoolReturnImpl {
        private static bool Prefix(
            SplitNativeArrayPool<byte> __instance, ref NativeArray<byte> arr
        ) {
            if (__instance is not FakeArrayPool) return true;
            arr.Dispose();
            return false;
        }
    }
}
