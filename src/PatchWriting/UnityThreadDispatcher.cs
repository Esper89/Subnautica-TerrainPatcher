using UnityEngine.Bindings;
using UWE;

namespace TerrainPatcher;

internal static class UnityThreadDispatcher
{
    private static UnityThread? unitySafeThread;
    private static int unityMainThreadID;
    
    /// <remarks>MUST be started from main thread for proper initialization</remarks>
    public static void StartUnityThread()
    {
        unityMainThreadID = Thread.CurrentThread.ManagedThreadId;
        unitySafeThread = ThreadUtils.StartUnityThread("TerrainPatcherMainThreadDispatcher", 5, Plugin.instance);
    }
    
    [ThreadSafe]
    internal static void EnsureOnUnityThread(Action action)
    {
        if (Thread.CurrentThread.ManagedThreadId == unityMainThreadID)
        {
            action.Invoke();
        }
        else
        {
            // TODO: might be better to roll our own threading so we dont have this delegate bs
            unitySafeThread?.Enqueue((_,_) => action.Invoke(), null, null);
        }
    }
}