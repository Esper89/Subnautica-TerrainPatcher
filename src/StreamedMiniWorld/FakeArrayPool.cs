using HarmonyLib;
using Unity.Collections;
using UWE;
using WorldStreaming;

namespace TerrainPatcher.StreamedMiniWorld;

/// <summary>To not overuse the game's allocator for terrain, this is a simple duplicate that,
/// counter to the original purpose, allocates on demand instead of pooling. Performance seems to be
/// in line with pooling, given meshes are not requested very often and allocations happen off the
/// main thread.</summary>
internal sealed class FakeArrayPool : SplitNativeArrayPool<byte> {
    internal FakeArrayPool() : base(0, 0, 0, 0, 0, 0, 0, 0) {
        SolidOctree = new(Octree.BytesPerNode, Allocator.Persistent);
        SolidOctree.CopyFrom(new byte[]{ 1, 0, 0, 0 });
    }
    
    [HarmonyPatch(typeof(SplitNativeArrayPool<byte>), nameof(Get))]
    private static class FakeArrayPoolGetImpl {
        private static bool Prefix(
            SplitNativeArrayPool<byte> __instance, int minLength, ref NativeArray<byte> __result
        ) {
            if (__instance is not FakeArrayPool) return true;
            __result = new(minLength, Allocator.Persistent);
            return false;
        }
    }

    [HarmonyPatch(typeof(SplitNativeArrayPool<byte>), nameof(Return))]
    private static class FakeArrayPoolReturnImpl {
        private static bool Prefix(
            SplitNativeArrayPool<byte> __instance, ref NativeArray<byte> arr
        ) {
            if (__instance is not FakeArrayPool fakeArrayPool) return true;
            if (arr == fakeArrayPool.SolidOctree) return false;
            arr.Dispose();
            return false;
        }
    }

    internal NativeArray<byte> SolidOctree { get; private set; }
}
