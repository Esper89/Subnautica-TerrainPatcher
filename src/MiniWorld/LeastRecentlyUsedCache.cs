using System.Collections;

namespace TerrainPatcher;

/// <summary>
/// A cache that stores keys paired to values.
/// Every time an item is used, it is moved to the front of the cache.
/// When the cache limit is exceeded, items at the end of the cache are evicted.
/// All access is guaranteed to be O(1)
/// </summary>
internal class LeastRecentlyUsedCache<K, V> : IEnumerable<V> {
    private readonly int _maxCapacity;
    private CacheNode? head;
    private CacheNode? tail;
    private int _currentSize;
    private readonly Dictionary<K, CacheNode> _nodeQuickMap;
    private readonly Action<V> _onRemoveElement;
    
    private class CacheNode(K _key, V _value) {
        internal K key = _key;
        internal V value = _value;
        internal CacheNode? next;
        internal CacheNode? prev;
    }
    
    public LeastRecentlyUsedCache(int maxCapacity, Action<V> onRemoveElement) {
        if (maxCapacity < 1) throw new ArgumentOutOfRangeException(nameof(maxCapacity));
        _maxCapacity = maxCapacity;
        _nodeQuickMap = new(maxCapacity);
        _onRemoveElement = onRemoveElement ?? throw new ArgumentNullException(nameof(onRemoveElement));
    }
    
    public bool TryGet(K key, out V? value) {
        if (_nodeQuickMap.TryGetValue(key, out CacheNode node)) {
            value = node.value;
            MoveToFront(node);
            return true;
        }
        value = default;
        return false;
    }

    public void Put(K key, V value) {
        Exception? callbackException = null;
        if (_nodeQuickMap.TryGetValue(key, out CacheNode node)) {
            callbackException = InvokeCallbackSafe(node.value);
            node.value = value;
            MoveToFront(node);
            if (callbackException != null) throw callbackException;
            return;
        }

        CacheNode newNode;
        if (_currentSize == _maxCapacity) {
            CacheNode removedNode = tail!;
            tail = removedNode.prev;
            tail?.next = null;
            _nodeQuickMap.Remove(removedNode.key);
            _currentSize--;
            callbackException = InvokeCallbackSafe(removedNode.value);
            //Reuse old removed node object
            newNode = removedNode;
            newNode.key = key;
            newNode.value = value;
        } else {
            newNode = new(key, value);
        }
        _nodeQuickMap.Add(key, newNode);
        if (_currentSize == 0) tail = newNode;
        _currentSize++;
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
        try { _onRemoveElement.Invoke(value); return null; }
        catch (Exception e) { return e; }
    }

    public IEnumerator<V> GetEnumerator() {
        foreach (CacheNode node in _nodeQuickMap.Values) {
            yield return node.value;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}