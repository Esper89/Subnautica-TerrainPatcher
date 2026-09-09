using System.Collections;
using Nautilus.Handlers;
using UnityEngine;

namespace TerrainPatcher.TerrainPatching;

internal static class PatchingThread {
    private static bool finishedPatching;

    internal static void BeginPatching() {
        _ = PatchTerrain();
    }

    private static async Task PatchTerrain() {
        await Task.Run(FileLoading.FindAndLoadPatches);
        finishedPatching = true;
    }

    internal static IEnumerator EnsurePatchingFinished(WaitScreenHandler.WaitScreenTask task) {
        yield return new WaitUntil(() => finishedPatching);
        MainThreadDispatcher.Stop();
    }
}
