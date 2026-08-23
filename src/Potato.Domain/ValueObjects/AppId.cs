namespace Potato.Domain.ValueObjects;

/// <summary>
/// Strongly-typed wrapper representing a Steam Application ID (AppID).
/// </summary>
public readonly record struct AppId : IComparable<AppId>
{
    public uint Value { get; }

    public bool IsValid => Value > 0;

    public static readonly AppId Empty = new(0);

    public AppId(uint value)
    {
        Value = value;
    }

    public static AppId From(uint value) => new(value);

    public static AppId Parse(string? input)
    {
        if (TryParse(input, out var appId))
        {
            return appId;
        }

        throw new FormatException($"'{input}' is not a valid Steam AppID.");
    }

    public static bool TryParse(string? input, out AppId appId)
    {
        appId = Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        if (uint.TryParse(trimmed, out var value) && value > 0)
        {
            appId = new AppId(value);
            return true;
        }

        return false;
    }

    public static implicit operator uint(AppId appId) => appId.Value;
    public static explicit operator AppId(uint value) => new(value);
    public static explicit operator AppId(int value) => value > 0 ? new AppId((uint)value) : Empty;

    public int CompareTo(AppId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();
}
