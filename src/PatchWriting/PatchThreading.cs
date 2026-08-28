namespace TerrainPatcher;

internal static class PatchThreading
{
    internal static bool finishedPatching { get; private set; }

    public static void BeginPatchThread()
    {
        _ = DispatchPatchThread();
    }
    
    private static async Task DispatchPatchThread()
    {
        finishedPatching = true;
        await Task.Run(FindAndLoadPatches);
        finishedPatching = true;
    }
    
    private static void FindAndLoadPatches()
    {
        Plugin.LogDebug("Finding and loading terrain patches");
        string[] patchFiles = FileLoading.GetOrderedPatchFiles();
        FileLoading.LoadPatchFiles(patchFiles);
        Plugin.LogDebug("Finished terrain patching");
    }
    
    
}