using System.Collections;
using Nautilus.Handlers;
using UnityEngine;

namespace TerrainPatcher.TerrainPatching;

internal static class PatchingThread {

    internal static Task BeginPatching() => PatchTerrain();

    private static async Task PatchTerrain() {
        await Task.Run(FileLoading.FindAndLoadPatches);
    }

    internal static IEnumerator EnsurePatchingFinished(Task patchingTask) {
        while (!patchingTask.IsCompleted) yield return null;
        
        if (patchingTask.IsFaulted) {
            Plugin.LogError($"Patching failed! {patchingTask.Exception?.InnerException}");
            throw new Exception("Patching Failed!");
        }
        
        MainThreadDispatcher.Stop();
    }
}
