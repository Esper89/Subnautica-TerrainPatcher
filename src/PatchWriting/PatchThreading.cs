using System.Collections;
using System.Diagnostics;
using Nautilus.Handlers;
using UnityEngine;

namespace TerrainPatcher;

internal static class PatchThreading
{
    private static bool finishedPatching { get; set; }

    public static void BeginPatchThread()
    {
        _ = DispatchPatchThread();
    }
    
    private static async Task DispatchPatchThread()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        await Task.Run(FileLoading.FindAndLoadPatches);
        stopwatch.Stop();
        Plugin.DisplayError($"Time To Patch: {stopwatch.ElapsedMilliseconds / 1000.0}s"); 
        finishedPatching = true;
    }

    public static IEnumerator EnsurePatchingFinished(WaitScreenHandler.WaitScreenTask task)
    {
        yield return new WaitUntil(() => finishedPatching);
        UnityThreadDispatcher.Stop();
    }
}