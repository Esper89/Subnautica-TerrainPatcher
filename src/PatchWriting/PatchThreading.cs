using System.Collections;
using Nautilus.Handlers;
using UnityEngine;

namespace TerrainPatcher;

internal static class PatchThreading {
    private static bool finishedPatching { get; set; }

    public static void BeginPatching() {
        _ = PatchThread();
    }

    private static async Task PatchThread() {
        await Task.Run(FileLoading.FindAndLoadPatches);
        finishedPatching = true;
    }

    public static IEnumerator EnsurePatchingFinished(WaitScreenHandler.WaitScreenTask task) {
        yield return new WaitUntil(() => finishedPatching);
        UnityThreadDispatcher.Stop();
    }
}
