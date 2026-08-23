using System.Text;

namespace Potato.Domain.Vdf;

/// <summary>
/// Serializer for converting VdfObject / VdfNode hierarchies to Valve KeyValue text representation.
/// Matches Steam's native tab-delimited formatting.
/// </summary>
public static class VdfSerializer
{
    public static string Serialize(VdfObject root, string? rootName = null)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(rootName))
        {
            sb.Append('"').Append(Escape(rootName)).Append("\"\n{\n");
            SerializeObjectChildren(root, sb, indentLevel: 1);
            sb.Append("}\n");
        }
        else
        {
            SerializeObjectChildren(root, sb, indentLevel: 0);
        }

        return sb.ToString();
    }

    private static void SerializeObjectChildren(VdfObject obj, StringBuilder sb, int indentLevel)
    {
        string indent = new('\t', indentLevel);

        foreach (var (key, node) in obj)
        {
            if (node is VdfValue valueNode)
            {
                sb.Append(indent)
                  .Append('"').Append(Escape(key)).Append("\"\t\t\"")
                  .Append(Escape(valueNode.Value)).Append("\"\n");
            }
            else if (node is VdfObject childObj)
            {
                sb.Append(indent)
                  .Append('"').Append(Escape(key)).Append("\"\n")
                  .Append(indent).Append("{\n");

                SerializeObjectChildren(childObj, sb, indentLevel + 1);

                sb.Append(indent).Append("}\n");
            }
        }
    }

    public static string Escape(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        var sb = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            switch (c)
            {
                case '\\': sb.Append(@"\\"); break;
                case '"': sb.Append(@"\"""); break;
                case '\n': sb.Append(@"\n"); break;
                case '\r': sb.Append(@"\r"); break;
                case '\t': sb.Append(@"\t"); break;
                default: sb.Append(c); break;
            }
        }

        return sb.ToString();
    }
}
