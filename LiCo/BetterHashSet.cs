using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;

namespace LiCo;
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
public class BetterHashSet<T> : HashSet<T>
{
    
}
#else
public class BetterHashSet<T> : ISet<T> where T : notnull
{
    private readonly Dictionary<T, T> _content = new();
    private readonly HashSet<T> _set = new();
    public HashSet<T>.Enumerator GetEnumerator()
    {
        return _set.GetEnumerator();
    }
    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    void ICollection<T>.Add(T item)
    {
        Add(item);
    }

    public void ExceptWith(IEnumerable<T> other)
    {
        foreach (var i in other)
            Remove(i);
    }

    private void CleanUp()
    {
        _content.Clear();
        foreach (var i in _set)
        {
            _content.Add(i, i);
        }
    }

    public void IntersectWith(IEnumerable<T> other)
    {
        _set.IntersectWith(other);
        CleanUp();
    }

    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        return _set.IsProperSubsetOf(other);
    }

    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        return _set.IsProperSupersetOf(other);
    }

    public bool IsSubsetOf(IEnumerable<T> other)
    {
        return _set.IsSubsetOf(other);
    }

    public bool IsSupersetOf(IEnumerable<T> other)
    {
        return _set.IsSupersetOf(other);
    }

    public bool Overlaps(IEnumerable<T> other)
    {
        return _set.Overlaps(other);
    }

    public bool SetEquals(IEnumerable<T> other)
    {
        return _set.SetEquals(other);
    }

    public void SymmetricExceptWith(IEnumerable<T> other)
    {
        foreach (var i in other)
        {
            if (!Remove(i))
            {
                Add(i);
            }
        }
    }

    public void UnionWith(IEnumerable<T> other)
    {
        foreach (var i in other)
        {
            Add(i);
        }
    }

    public bool Add(T item)
    {
        if (!_set.Add(item))
            return false;
        _content.Add(item, item);
        return true;
    }

    public void Clear()
    {
        _content.Clear();
        _set.Clear();
    }

    public bool Contains(T item)
    {
        return _set.Contains(item);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        _set.CopyTo(array, arrayIndex);
    }

    public bool Remove(T item)
    {
        _ = _content.Remove(item);
        return _set.Remove(item);
    }

    public bool TryGetValue(T value, out T originalValue)
    {
        return _content.TryGetValue(value, out originalValue);
    }

    public int Count => _set.Count;
    public bool IsReadOnly => ((ICollection<KeyValuePair<T, T>>)_content).IsReadOnly;
}
#endif