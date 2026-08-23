namespace Potato.Domain.ValueObjects;

/// <summary>
/// Strongly-typed wrapper representing a 64-bit Steam Manifest Global ID (GID).
/// </summary>
public readonly record struct ManifestGid : IComparable<ManifestGid>
{
    public ulong Value { get; }

    public bool IsValid => Value > 0;

    public static readonly ManifestGid Empty = new(0);

    public ManifestGid(ulong value)
    {
        Value = value;
    }

    public static ManifestGid From(ulong value) => new(value);

    public static ManifestGid Parse(string? input)
    {
        if (TryParse(input, out var gid))
        {
            return gid;
        }

        throw new FormatException($"'{input}' is not a valid Steam Manifest GID.");
    }

    public static bool TryParse(string? input, out ManifestGid gid)
    {
        gid = Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        if (ulong.TryParse(trimmed, out var value) && value > 0)
        {
            gid = new ManifestGid(value);
            return true;
        }

        return false;
    }

    public static implicit operator ulong(ManifestGid gid) => gid.Value;
    public static explicit operator ManifestGid(ulong value) => new(value);

    public int CompareTo(ManifestGid other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();
}
