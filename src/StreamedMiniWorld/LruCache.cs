using System.Collections;

namespace TerrainPatcher.StreamedMiniWorld;

/// <summary>A cache that stores keys paired to values. Every time an item is used, it is moved to
/// the front of the cache. When the cache limit is exceeded, items at the end of the cache are
/// evicted.</summary>
internal sealed class LruCache<K, V> : IEnumerable<V> {
    private readonly int maxCapacity;
    private CacheNode? head;
    private CacheNode? tail;
    private int currentSize;
    private readonly Dictionary<K, CacheNode> nodeQuickMap;
    private readonly Action<V> onRemoveElement;

    private sealed class CacheNode(K key, V value) {
        internal K key = key;
        internal V value = value;
        internal CacheNode? next;
        internal CacheNode? prev;
    }

    internal LruCache(int maxCapacity, Action<V> onRemoveElement) {
        if (maxCapacity < 1) throw new ArgumentOutOfRangeException(nameof(maxCapacity));
        this.maxCapacity = maxCapacity;
        nodeQuickMap = new(maxCapacity);
        this.onRemoveElement = onRemoveElement
            ?? throw new ArgumentNullException(nameof(onRemoveElement));
    }

    internal bool TryGet(K key, out V? value) {
        if (nodeQuickMap.TryGetValue(key, out CacheNode node)) {
            value = node.value;
            MoveToFront(node);
            return true;
        }
        value = default;
        return false;
    }

    internal void Put(K key, V value) {
        Exception? callbackException = null;
        if (nodeQuickMap.TryGetValue(key, out CacheNode node)) {
            callbackException = InvokeCallbackSafe(node.value);
            node.value = value;
            MoveToFront(node);
            if (callbackException != null) throw callbackException;
            return;
        }

        CacheNode newNode;
        if (currentSize == maxCapacity) {
            CacheNode removedNode = tail!;
            tail = removedNode.prev;
            tail?.next = null;
            nodeQuickMap.Remove(removedNode.key);
            currentSize--;
            callbackException = InvokeCallbackSafe(removedNode.value);

            // reuse old removed node object
            newNode = removedNode;
            newNode.key = key;
            newNode.value = value;
        } else {
            newNode = new(key, value);
        }
        nodeQuickMap.Add(key, newNode);
        if (currentSize == 0) tail = newNode;
        currentSize++;
        AddFront(newNode);
        if (callbackException != null) throw callbackException;
    }

    private void MoveToFront(CacheNode node) {
        if (node == head) return;
        node.prev?.next = node.next;
        node.next?.prev = node.prev;
        if (node == tail) tail = node.prev;
        AddFront(node);
    }

    private void AddFront(CacheNode node) {
        head?.prev = node;
        node.next = head;
        node.prev = null;
        head = node;
    }

    private Exception? InvokeCallbackSafe(V value) {
        try { onRemoveElement.Invoke(value); return null; }
        catch (Exception ex) { return ex; }
    }

    public IEnumerator<V> GetEnumerator() {
        foreach (CacheNode node in nodeQuickMap.Values) {
            yield return node.value;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
