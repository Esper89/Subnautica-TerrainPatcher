namespace TerrainPatcher.TerrainPatching;

internal static class PatchingThread {
    private static Task? PATCHING = null;

    internal static void BeginPatching() => PATCHING = Task.Run(PatchThread);

    private static void PatchThread() {
        try {
            OptoctreesDirs.ClearPatchesDir();
            FileLoading.FindAndLoadPatches();
        } catch (Exception ex) {
            Plugin.LogFatal($"Patching failed unexpectedly: {ex}");
            Plugin.DisplayError("Terrain patching failed!");
            throw new Exception("Terrain patching failed!");
        }
    }

    internal static bool IsDone => PATCHING?.IsCompleted ?? false;

    internal static void BlockUntilDone() => PATCHING!.Wait();

    // wait for 1 ms instead of checking if it's done to avoid possible priority inversion
    internal static bool PollDone() => PATCHING?.Wait(1) ?? false;
}
