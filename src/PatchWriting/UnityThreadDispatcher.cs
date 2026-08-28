using System.Collections;
using System.Collections.Concurrent;
using UnityEngine;

namespace TerrainPatcher;

internal static class UnityThreadDispatcher
{
    private static int unityMainThreadID;
    private static MonoBehaviour? routineHost;
    private static Coroutine? coroutineLoop;
    private static readonly ConcurrentQueue<Action> tasks = new();
    
    public static void Start(MonoBehaviour host)
    {
        if (routineHost != null) throw new InvalidOperationException("Dispatcher already initialized");
        unityMainThreadID = Thread.CurrentThread.ManagedThreadId;
        routineHost = host;
        coroutineLoop = host.StartCoroutine(ExecuteUnityThreadTasks());
    }

    public static void Stop()
    {
        routineHost?.StopCoroutine(coroutineLoop);
        coroutineLoop = null;
        routineHost = null;
    }
    
    internal static void EnsureOnUnityThread(Action action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
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
        for (;;)
        {
            while (tasks.TryDequeue(out Action task))
            {
                task.Invoke();
            }
            yield return null;
        }
    }
}