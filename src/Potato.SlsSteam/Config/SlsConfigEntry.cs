namespace Potato.SlsSteam.Config;

/// <summary>
/// Represents an entry in a YAML list or mapping with preserved inline comment.
/// </summary>
public sealed record SlsConfigEntry(string? Key, string Value, string? InlineComment = null)
{
    public string FormattedValue => !string.IsNullOrWhiteSpace(InlineComment)
        ? $"{Value} # {InlineComment.Trim()}"
        : Value;

    public override string ToString() => FormattedValue;
}
