using System.Collections;
using Nautilus.Handlers;

namespace TerrainPatcher.Integrations;

internal static class Nautilus {
    internal static void RegisterWaitScreenHandler() {
        static IEnumerator EnsurePatchingFinished() {
            while (!TerrainPatching.PatchingThread.PollDone()) yield return null;
        }

        WaitScreenHandler.RegisterAsyncLoadTask(
            modName: "Terrain Patcher",
            loadingFunction: _ => EnsurePatchingFinished(),
            description: "Patching Terrain"
        );
    }
}
