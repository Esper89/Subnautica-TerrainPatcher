namespace TerrainPatcher;

internal static class PatchThreading
{
    private static bool isPatchThreadActive { get; set; }

    public static void BeginPatchThread()
    {
        _ = DispatchPatchThread();
    }
    
    private static async Task DispatchPatchThread()
    {
        isPatchThreadActive = true;
        await Task.Run(FindAndLoadPatches);
        isPatchThreadActive = true;
    }
    
    private static void FindAndLoadPatches()
    {
        Plugin.LogDebug("Finding and loading terrain patches");
        string[] patchFiles = FileLoading.GetOrderedPatchFiles();
        FileLoading.LoadPatchFiles(patchFiles);
        Plugin.LogDebug("Finished terrain patching");
    }
}