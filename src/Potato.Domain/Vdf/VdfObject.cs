using System.Collections;

namespace Potato.Domain.Vdf;

/// <summary>
/// Represents a structured object node in a Valve VDF document containing ordered child key-value pairs.
/// </summary>
public sealed class VdfObject : VdfNode, IEnumerable<KeyValuePair<string, VdfNode>>
{
    private readonly List<KeyValuePair<string, VdfNode>> _entries = new();

    public override bool IsObject => true;
    public override bool IsValue => false;

    public int Count => _entries.Count;

    public VdfNode this[string key]
    {
        get
        {
            if (TryGet(key, out var node))
            {
                return node;
            }

            throw new KeyNotFoundException($"Key '{key}' was not found in VdfObject.");
        }
        set => Set(key, value);
    }

    public bool ContainsKey(string key) =>
        _entries.Any(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));

    public bool TryGet(string key, out VdfNode node)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (string.Equals(_entries[i].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                node = _entries[i].Value;
                return true;
            }
        }

        node = null!;
        return false;
    }

    public bool TryGetValue(string key, out string value)
    {
        if (TryGet(key, out var node) && node is VdfValue vdfVal)
        {
            value = vdfVal.Value;
            return true;
        }

        value = string.Empty;
        return false;
    }

    public bool TryGetObject(string key, out VdfObject obj)
    {
        if (TryGet(key, out var node) && node is VdfObject vdfObj)
        {
            obj = vdfObj;
            return true;
        }

        obj = null!;
        return false;
    }

    public string GetString(string key, string defaultValue = "")
    {
        if (TryGetValue(key, out var val))
        {
            return val;
        }

        return defaultValue;
    }

    public int GetInt32(string key, int defaultValue = 0)
    {
        if (TryGetValue(key, out var val) && int.TryParse(val, out var res))
        {
            return res;
        }

        return defaultValue;
    }

    public uint GetUInt32(string key, uint defaultValue = 0)
    {
        if (TryGetValue(key, out var val) && uint.TryParse(val, out var res))
        {
            return res;
        }

        return defaultValue;
    }

    public long GetInt64(string key, long defaultValue = 0)
    {
        if (TryGetValue(key, out var val) && long.TryParse(val, out var res))
        {
            return res;
        }

        return defaultValue;
    }

    public ulong GetUInt64(string key, ulong defaultValue = 0)
    {
        if (TryGetValue(key, out var val) && ulong.TryParse(val, out var res))
        {
            return res;
        }

        return defaultValue;
    }

    public VdfObject? GetObject(string key)
    {
        if (TryGetObject(key, out var obj))
        {
            return obj;
        }

        return null;
    }

    public VdfObject Set(string key, VdfNode node)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (node == null) throw new ArgumentNullException(nameof(node));

        for (int i = 0; i < _entries.Count; i++)
        {
            if (string.Equals(_entries[i].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                _entries[i] = new KeyValuePair<string, VdfNode>(key, node);
                return this;
            }
        }

        _entries.Add(new KeyValuePair<string, VdfNode>(key, node));
        return this;
    }

    public VdfObject Set(string key, string value) => Set(key, new VdfValue(value));

    public bool Remove(string key)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (string.Equals(_entries[i].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                _entries.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    public void Clear() => _entries.Clear();

    public IEnumerator<KeyValuePair<string, VdfNode>> GetEnumerator() => _entries.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
