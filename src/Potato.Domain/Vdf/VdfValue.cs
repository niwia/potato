namespace Potato.Domain.Vdf;

/// <summary>
/// Represents a scalar string KeyValue in a Valve VDF document.
/// </summary>
public sealed class VdfValue : VdfNode, IEquatable<VdfValue>
{
    public string Value { get; set; }

    public override bool IsObject => false;
    public override bool IsValue => true;

    public VdfValue(string value)
    {
        Value = value ?? string.Empty;
    }

    public bool TryGetInt32(out int result) => int.TryParse(Value, out result);
    public bool TryGetUInt32(out uint result) => uint.TryParse(Value, out result);
    public bool TryGetInt64(out long result) => long.TryParse(Value, out result);
    public bool TryGetUInt64(out ulong result) => ulong.TryParse(Value, out result);
    public bool TryGetBoolean(out bool result)
    {
        if (Value == "1" || string.Equals(Value, "true", StringComparison.OrdinalIgnoreCase))
        {
            result = true;
            return true;
        }

        if (Value == "0" || string.Equals(Value, "false", StringComparison.OrdinalIgnoreCase))
        {
            result = false;
            return true;
        }

        result = false;
        return false;
    }

    public override string ToString() => Value;

    public bool Equals(VdfValue? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Value == other.Value;
    }

    public override bool Equals(object? obj) => Equals(obj as VdfValue);

    public override int GetHashCode() => Value.GetHashCode();
}
