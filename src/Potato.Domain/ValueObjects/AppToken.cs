namespace Potato.Domain.ValueObjects;

/// <summary>
/// Strongly-typed wrapper representing a Steam AppToken used for authenticated depot/product info access.
/// </summary>
public readonly record struct AppToken : IComparable<AppToken>
{
    public ulong Value { get; }

    public bool IsValid => Value > 0;

    public static readonly AppToken Empty = new(0);

    public AppToken(ulong value)
    {
        Value = value;
    }

    public static AppToken From(ulong value) => new(value);

    public static AppToken Parse(string? input)
    {
        if (TryParse(input, out var token))
        {
            return token;
        }

        throw new FormatException($"'{input}' is not a valid Steam AppToken.");
    }

    public static bool TryParse(string? input, out AppToken token)
    {
        token = Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim().Trim('"', '\'');
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..];
        }

        if (ulong.TryParse(trimmed, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var decValue) && decValue > 0)
        {
            token = new AppToken(decValue);
            return true;
        }

        if (ulong.TryParse(trimmed, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var hexValue) && hexValue > 0)
        {
            token = new AppToken(hexValue);
            return true;
        }

        return false;
    }

    public static implicit operator ulong(AppToken token) => token.Value;
    public static explicit operator AppToken(ulong value) => new(value);

    public int CompareTo(AppToken other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();
}
