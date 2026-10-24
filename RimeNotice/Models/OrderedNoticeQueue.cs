using System;
using System.Collections.Generic;
using System.Linq;
namespace RimeNotice.Models;

// Preserve each accepted message; never merge different bodies by toast ID.
public sealed class OrderedNoticeQueue<T>(int capacity = 512)
{
    private readonly Queue<(long Sequence, string Key, T Item)> _items = new();
    private readonly HashSet<string> _keys = new(StringComparer.Ordinal);
    private long _sequence;
    public int Count => _items.Count;
    public bool Enqueue(string key, T item)
    {
        if (_keys.Contains(key) || _items.Count >= capacity) return false;
        _keys.Add(key); _items.Enqueue((++_sequence, key, item)); return true;
    }
    public bool TryDequeue(out T item)
    {
        if (!_items.TryDequeue(out var entry)) { item = default!; return false; }
        _keys.Remove(entry.Key); item = entry.Item; return true;
    }
    public T[] Preview(int count) => _items.Take(count).Select(e => e.Item).ToArray();
    public void Clear() { _items.Clear(); _keys.Clear(); }
}
