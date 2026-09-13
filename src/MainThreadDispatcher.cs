using System.Collections;
using System.Collections.Concurrent;
using UnityEngine;

namespace TerrainPatcher;

internal static class MainThreadDispatcher {
    private static int MAIN_THREAD_ID;
    private static MonoBehaviour? ROUTINE_HOST;
    private static Coroutine? COROUTINE_LOOP;
    private static readonly ConcurrentQueue<Action> TASKS = new();

    internal static void Start(MonoBehaviour host) {
        if (ROUTINE_HOST != null) {
            throw new InvalidOperationException("Dispatcher already initialized");
        }
        MAIN_THREAD_ID = Thread.CurrentThread.ManagedThreadId;
        ROUTINE_HOST = host;
        COROUTINE_LOOP = host.StartCoroutine(ExecuteMainThreadTasks());
    }

    internal static void EnsureOnMainThread(Action action) {
        if (action == null) throw new ArgumentNullException(nameof(action));
        if (Thread.CurrentThread.ManagedThreadId == MAIN_THREAD_ID) {
            TryInvoke(action);
        } else {
            TASKS.Enqueue(action);
        }
    }

    private static IEnumerator ExecuteMainThreadTasks() {
        for (;;) {
            while (TASKS.TryDequeue(out Action action)) TryInvoke(action);
            if (TerrainPatching.PatchingThread.PollDone()) break;
            yield return null;
        }
        COROUTINE_LOOP = null;
        ROUTINE_HOST = null;
    }

    private static void TryInvoke(Action action) {
        try {
            action.Invoke();
        } catch (Exception ex) {
            Plugin.LogError($"Main thread task threw an exception: {ex}");
        }
    }
}
