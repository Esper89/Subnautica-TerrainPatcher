using System.Collections;
using System.Collections.Concurrent;
using UnityEngine;

namespace TerrainPatcher;

internal static class UnityThreadDispatcher
{
    private static int unityThreadID;
    private static MonoBehaviour? routineHost;
    private static Coroutine? coroutineLoop;
    private static readonly ConcurrentQueue<Action> tasks = new();
    
    public static void Start(MonoBehaviour host) {
        if (routineHost != null) throw new InvalidOperationException("Dispatcher already initialized");
        unityThreadID = Thread.CurrentThread.ManagedThreadId;
        routineHost = host;
        coroutineLoop = host.StartCoroutine(ExecuteUnityThreadTasks());
    }

    public static void Stop() {
        routineHost?.StopCoroutine(coroutineLoop);
        coroutineLoop = null;
        routineHost = null;
    }
    
    internal static void EnsureOnUnityThread(Action action) {
        if (action == null) throw new ArgumentNullException(nameof(action));
        if (Thread.CurrentThread.ManagedThreadId == unityThreadID) {
            TryInvoke(action);
        }
        else {
            tasks.Enqueue(action);
        }
    }

    private static IEnumerator ExecuteUnityThreadTasks() {
        for (;;) {
            while (tasks.TryDequeue(out Action action)) {
                TryInvoke(action);
            }
            yield return null;
        }
    }

    private static void TryInvoke(Action action) {
        try {
            action.Invoke();
        }
        catch (Exception e) {
            Plugin.LogError($"Main thread task threw and exception: {e}");
        }
    }
}