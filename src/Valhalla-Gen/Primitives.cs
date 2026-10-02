using System.Text.RegularExpressions;

namespace Valhalla.Gen;

// ValhallaPrimitives.def is the single source of truth; aliases only map spellings onto names it already declares.
sealed partial class PrimitiveTable
{
    readonly HashSet<string> m_Names = new(StringComparer.Ordinal);
    readonly Dictionary<string, string> m_Aliases = new(StringComparer.Ordinal);

    [GeneratedRegex(@"^\s*VALHALLA_PRIMITIVE\((\w+)\)", RegexOptions.Multiline)]
    private static partial Regex PrimitiveRegex();

    public static PrimitiveTable Load(string defPath)
    {
        var table = new PrimitiveTable();
        foreach (Match m in PrimitiveRegex().Matches(File.ReadAllText(defPath)))
            table.m_Names.Add(m.Groups[1].Value);

        void Alias(string spelling, string target)
        {
            if (table.m_Names.Contains(target)) table.m_Aliases[spelling] = target;
        }

        Alias("int", "int32_t");
        Alias("signed", "int32_t");
        Alias("signed int", "int32_t");
        Alias("unsigned", "uint32_t");
        Alias("unsigned int", "uint32_t");
        Alias("short", "int16_t");
        Alias("short int", "int16_t");
        Alias("signed short", "int16_t");
        Alias("unsigned short", "uint16_t");
        Alias("unsigned short int", "uint16_t");
        Alias("long long", "int64_t");
        Alias("long long int", "int64_t");
        Alias("signed long long", "int64_t");
        Alias("unsigned long long", "uint64_t");
        Alias("unsigned long long int", "uint64_t");
        Alias("signed char", "int8_t");
        Alias("unsigned char", "uint8_t");
        Alias("size_t", "uint64_t");
        Alias("ptrdiff_t", "int64_t");
        return table;
    }

    public IReadOnlyCollection<string> Names => m_Names;

    public string? Normalize(string spelling)
    {
        string s = spelling.StartsWith("::") ? spelling[2..] : spelling;
        if (s.StartsWith("std::")) s = s[5..];
        if (m_Names.Contains(s)) return s;
        return m_Aliases.TryGetValue(s, out var target) ? target : null;
    }
}
