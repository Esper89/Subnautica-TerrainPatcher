using HarmonyLib;
using Unity.Collections;
using UWE;

namespace TerrainPatcher;

/// <summary>
/// As to not overuse the base game allocator for terrain, this is a simple duplicate that
/// (counter to the original purpose) allocates on demand (instead of pooling).
/// Performance seems to be inline with pooling, given meshes are not requested that
/// often and allocations happen off main thread.
/// </summary>
[HarmonyPatch(typeof(SplitNativeArrayPool<byte>))]
public class MiniWorldArrayPool() : SplitNativeArrayPool<byte>(0, 0, 0, 0, 0, 0, 0, 0) {
    [HarmonyPatch(typeof(SplitNativeArrayPool<byte>), nameof(Get))]
    [HarmonyPrefix]
    public static bool Get_PreFix(SplitNativeArrayPool<byte> __instance, int minLength, ref NativeArray<byte> __result) {
        if (__instance is not MiniWorldArrayPool) return true;
        __result = new(minLength, Allocator.Persistent);
        return false;
    }
    
    [HarmonyPatch(typeof(SplitNativeArrayPool<byte>), nameof(Return))]
    [HarmonyPrefix]
    public static bool Return_PreFix(SplitNativeArrayPool<byte> __instance, ref NativeArray<byte> arr) {
        if (__instance is not MiniWorldArrayPool) return true;
        arr.Dispose();
        return false;
    }
}