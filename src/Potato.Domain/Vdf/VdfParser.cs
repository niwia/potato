namespace Potato.Domain.Vdf;

/// <summary>
/// Recursive-descent parser for Valve KeyValue (VDF/ACF) documents.
/// </summary>
public sealed class VdfParser
{
    private readonly VdfTokenizer _tokenizer;
    private VdfToken _currentToken;

    public VdfParser(string text)
    {
        _tokenizer = new VdfTokenizer(text);
        _currentToken = _tokenizer.NextToken();
    }

    /// <summary>
    /// Parses the VDF text into a root VdfObject.
    /// </summary>
    public static VdfObject Parse(string text)
    {
        var parser = new VdfParser(text);
        return parser.ParseRoot();
    }

    private VdfObject ParseRoot()
    {
        var root = new VdfObject();

        while (_currentToken.Type != VdfTokenType.EndOfFile)
        {
            if (_currentToken.Type != VdfTokenType.String)
            {
                throw new FormatException(
                    $"Unexpected token '{_currentToken.Value}' ({_currentToken.Type}) at line {_currentToken.Line}, column {_currentToken.Column}. Expected key string.");
            }

            string key = _currentToken.Value;
            Consume(VdfTokenType.String);

            if (_currentToken.Type == VdfTokenType.OpenBrace)
            {
                var childObj = ParseObject();
                root.Set(key, childObj);
            }
            else if (_currentToken.Type == VdfTokenType.String)
            {
                string value = _currentToken.Value;
                Consume(VdfTokenType.String);
                root.Set(key, new VdfValue(value));
            }
            else
            {
                throw new FormatException(
                    $"Unexpected token '{_currentToken.Value}' ({_currentToken.Type}) at line {_currentToken.Line}, column {_currentToken.Column}. Expected value or '{{'.");
            }
        }

        return root;
    }

    private VdfObject ParseObject()
    {
        Consume(VdfTokenType.OpenBrace);
        var obj = new VdfObject();

        while (_currentToken.Type != VdfTokenType.CloseBrace && _currentToken.Type != VdfTokenType.EndOfFile)
        {
            if (_currentToken.Type != VdfTokenType.String)
            {
                throw new FormatException(
                    $"Unexpected token '{_currentToken.Value}' ({_currentToken.Type}) at line {_currentToken.Line}, column {_currentToken.Column}. Expected key string in object.");
            }

            string key = _currentToken.Value;
            Consume(VdfTokenType.String);

            if (_currentToken.Type == VdfTokenType.OpenBrace)
            {
                var childObj = ParseObject();
                obj.Set(key, childObj);
            }
            else if (_currentToken.Type == VdfTokenType.String)
            {
                string value = _currentToken.Value;
                Consume(VdfTokenType.String);
                obj.Set(key, new VdfValue(value));
            }
            else
            {
                throw new FormatException(
                    $"Unexpected token '{_currentToken.Value}' ({_currentToken.Type}) at line {_currentToken.Line}, column {_currentToken.Column}. Expected value string or '{{'.");
            }
        }

        Consume(VdfTokenType.CloseBrace);
        return obj;
    }

    private void Consume(VdfTokenType expected)
    {
        if (_currentToken.Type != expected)
        {
            throw new FormatException(
                $"Syntax error at line {_currentToken.Line}, column {_currentToken.Column}. Expected {expected} but found {_currentToken.Type} ('{_currentToken.Value}').");
        }

        _currentToken = _tokenizer.NextToken();
    }
}
