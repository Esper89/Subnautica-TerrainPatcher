using System.Collections;
using System.Collections.Concurrent;
using UnityEngine.Bindings;

namespace TerrainPatcher;

internal static class UnityThreadDispatcher
{
    private static int unityMainThreadID;
    private static readonly ConcurrentQueue<Action> tasks = new();
    
    /// <remarks>MUST be started from main thread for proper initialization</remarks>
    public static void StartUnityThread()
    {
        unityMainThreadID = Thread.CurrentThread.ManagedThreadId;
        Plugin.instance.StartCoroutine(ExecuteUnityThreadTasks());
    }
    
    [ThreadSafe]
    internal static void EnsureOnUnityThread(Action action)
    {
        if(action == null) throw new ArgumentNullException(nameof(action));
        if (Thread.CurrentThread.ManagedThreadId == unityMainThreadID)
        {
            action.Invoke();
        }
        else
        {
            tasks.Enqueue(action);
        }
    }

    private static IEnumerator ExecuteUnityThreadTasks()
    {
        yield return null;
        while (tasks.TryDequeue(out Action task))
        {
            task.Invoke();
        }
    }
}