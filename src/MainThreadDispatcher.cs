using System.Collections;
using System.Collections.Concurrent;
using UnityEngine;

namespace TerrainPatcher;

internal static class MainThreadDispatcher {
    private static int mainThreadId;
    private static MonoBehaviour? routineHost;
    private static Coroutine? coroutineLoop;
    private static readonly ConcurrentQueue<Action> tasks = new();

    internal static void Start(MonoBehaviour host) {
        if (routineHost != null) {
            throw new InvalidOperationException("Dispatcher already initialized");
        }
        mainThreadId = Thread.CurrentThread.ManagedThreadId;
        routineHost = host;
        coroutineLoop = host.StartCoroutine(ExecuteMainThreadTasks());
    }

    internal static void Stop() {
        routineHost?.StopCoroutine(coroutineLoop);
        coroutineLoop = null;
        routineHost = null;
    }

    internal static void EnsureOnMainThread(Action action) {
        if (action == null) throw new ArgumentNullException(nameof(action));
        if (Thread.CurrentThread.ManagedThreadId == mainThreadId) {
            TryInvoke(action);
        } else {
            tasks.Enqueue(action);
        }
    }

    private static IEnumerator ExecuteMainThreadTasks() {
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
        } catch (Exception ex) {
            Plugin.LogError($"Main thread task threw and exception: {ex}");
        }
    }
}
