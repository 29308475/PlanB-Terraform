using System;
using System.Collections.Generic;

namespace Roadlife
{
    internal sealed class BoundedCache<TKey, TValue>
    {
        private readonly int capacity;
        private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> entries = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>();
        private readonly LinkedList<KeyValuePair<TKey, TValue>> order = new LinkedList<KeyValuePair<TKey, TValue>>();
        internal BoundedCache(int capacity) { if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity)); this.capacity = capacity; }
        internal int Count => entries.Count;
        internal bool TryGet(TKey key, out TValue value)
        {
            if (!entries.TryGetValue(key, out var node)) { value = default(TValue); return false; }
            order.Remove(node); order.AddLast(node); value = node.Value.Value; return true;
        }
        internal void Put(TKey key, TValue value)
        {
            Remove(key);
            var node = order.AddLast(new KeyValuePair<TKey, TValue>(key, value)); entries[key] = node;
            if (entries.Count <= capacity) return;
            entries.Remove(order.First.Value.Key); order.RemoveFirst();
        }
        internal void Remove(TKey key)
        { if (entries.TryGetValue(key, out var node)) { entries.Remove(key); order.Remove(node); } }
        internal void Clear() { entries.Clear(); order.Clear(); }
    }
}
