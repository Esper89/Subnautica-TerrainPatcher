using System.Collections;

namespace TerrainPatcher.TerrainPatching;

internal static class PatchingThread {
    internal static Task BeginPatching() => PatchTerrain();

    private static async Task PatchTerrain() {
        await Task.Run(PatchThread);
    }

    private static Task PatchThread() {
        OptoctreesDirs.ClearPatchesDir();
        FileLoading.FindAndLoadPatches();
        return Task.CompletedTask;
    }

    internal static IEnumerator EnsurePatchingFinished(Task patchingTask) {
        while (!patchingTask.IsCompleted) yield return null;

        if (patchingTask.IsFaulted) {
            Plugin.LogError($"Patching failed: {patchingTask.Exception?.InnerException}");
            throw new Exception("Patching failed!");
        }

        MainThreadDispatcher.Stop();
    }
}
