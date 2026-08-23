namespace Potato.Domain.ValueObjects;

/// <summary>
/// Strongly-typed wrapper representing a Steam Depot ID.
/// </summary>
public readonly record struct DepotId : IComparable<DepotId>
{
    public uint Value { get; }

    public bool IsValid => Value > 0;

    public static readonly DepotId Empty = new(0);

    public DepotId(uint value)
    {
        Value = value;
    }

    public static DepotId From(uint value) => new(value);

    public static DepotId Parse(string? input)
    {
        if (TryParse(input, out var depotId))
        {
            return depotId;
        }

        throw new FormatException($"'{input}' is not a valid Steam Depot ID.");
    }

    public static bool TryParse(string? input, out DepotId depotId)
    {
        depotId = Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        if (uint.TryParse(trimmed, out var value) && value > 0)
        {
            depotId = new DepotId(value);
            return true;
        }

        return false;
    }

    public static implicit operator uint(DepotId depotId) => depotId.Value;
    public static explicit operator DepotId(uint value) => new(value);
    public static explicit operator DepotId(int value) => value > 0 ? new DepotId((uint)value) : Empty;

    public int CompareTo(DepotId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();
}
