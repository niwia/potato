using System.Text;

namespace Potato.Domain.Vdf;

public enum VdfTokenType
{
    None,
    String,
    OpenBrace,
    CloseBrace,
    EndOfFile
}

public readonly record struct VdfToken(VdfTokenType Type, string Value, int Line, int Column);

/// <summary>
/// Fast tokenizer for Valve KeyValue (VDF) format with support for quoted strings,
/// escape sequences, line comments (//), and unquoted identifier tokens.
/// </summary>
public sealed class VdfTokenizer
{
    private readonly string _text;
    private int _index;
    private int _line = 1;
    private int _column = 1;

    public VdfTokenizer(string text)
    {
        _text = text ?? string.Empty;
        _index = 0;
    }

    public VdfToken NextToken()
    {
        while (_index < _text.Length)
        {
            char c = _text[_index];

            // Whitespace handling
            if (char.IsWhiteSpace(c))
            {
                Advance();
                continue;
            }

            // Comments (// ...)
            if (c == '/' && _index + 1 < _text.Length && _text[_index + 1] == '/')
            {
                SkipLineComment();
                continue;
            }

            int tokenLine = _line;
            int tokenCol = _column;

            // Structure tokens
            if (c == '{')
            {
                Advance();
                return new VdfToken(VdfTokenType.OpenBrace, "{", tokenLine, tokenCol);
            }

            if (c == '}')
            {
                Advance();
                return new VdfToken(VdfTokenType.CloseBrace, "}", tokenLine, tokenCol);
            }

            // Quoted String
            if (c == '"')
            {
                string strVal = ReadQuotedString();
                return new VdfToken(VdfTokenType.String, strVal, tokenLine, tokenCol);
            }

            // Unquoted / Bare String
            string bareVal = ReadBareString();
            return new VdfToken(VdfTokenType.String, bareVal, tokenLine, tokenCol);
        }

        return new VdfToken(VdfTokenType.EndOfFile, string.Empty, _line, _column);
    }

    private void Advance()
    {
        if (_index >= _text.Length) return;

        char c = _text[_index];
        _index++;

        if (c == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }
    }

    private void SkipLineComment()
    {
        while (_index < _text.Length && _text[_index] != '\n')
        {
            Advance();
        }

        if (_index < _text.Length && _text[_index] == '\n')
        {
            Advance();
        }
    }

    private string ReadQuotedString()
    {
        Advance(); // Skip opening quote
        var sb = new StringBuilder();

        while (_index < _text.Length)
        {
            char c = _text[_index];

            if (c == '\\' && _index + 1 < _text.Length)
            {
                Advance(); // Skip backslash
                char next = _text[_index];
                switch (next)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case '\\': sb.Append('\\'); break;
                    case '"': sb.Append('"'); break;
                    default:
                        sb.Append('\\');
                        sb.Append(next);
                        break;
                }
                Advance();
                continue;
            }

            if (c == '"')
            {
                Advance(); // Skip closing quote
                return sb.ToString();
            }

            sb.Append(c);
            Advance();
        }

        return sb.ToString();
    }

    private string ReadBareString()
    {
        var sb = new StringBuilder();

        while (_index < _text.Length)
        {
            char c = _text[_index];

            if (char.IsWhiteSpace(c) || c == '{' || c == '}' || c == '"' || (c == '/' && _index + 1 < _text.Length && _text[_index + 1] == '/'))
            {
                break;
            }

            sb.Append(c);
            Advance();
        }

        return sb.ToString();
    }
}
