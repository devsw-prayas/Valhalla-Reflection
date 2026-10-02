using System.Text;

namespace Valhalla.Gen;

enum TokenKind : byte { Identifier, Number, String, Char, Punct }

sealed record Token(TokenKind Kind, string Text, int Line)
{
    public bool Is(string text) => Text == text;
    public bool IsIdent => Kind == TokenKind.Identifier;
    public override string ToString() => Text;
}

// Preprocessor lines are dropped wholesale: annotations are plain macros, never inside directives.
static class Lexer
{
    // '>' is deliberately absent from multi-char punctuators so nested template closers ('>>') stay separate tokens.
    static readonly string[] MultiCharPunct =
    [
        "<=>", "<<=", "->*", "...", "::", "->", "++", "--", "<<", "<=", "==", "!=", "&&", "||",
        "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", ".*",
    ];

    public static List<Token> Tokenize(string source)
    {
        var tokens = new List<Token>();
        int i = 0, line = 1;
        bool lineStart = true;

        while (i < source.Length)
        {
            char c = source[i];

            if (c == '\n') { line++; i++; lineStart = true; continue; }
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (c == '#' && lineStart)
            {
                while (i < source.Length && source[i] != '\n')
                {
                    if (source[i] == '\\' && i + 1 < source.Length && source[i + 1] == '\n') { line++; i += 2; continue; }
                    if (source[i] == '\\' && i + 2 < source.Length && source[i + 1] == '\r' && source[i + 2] == '\n') { line++; i += 3; continue; }
                    i++;
                }
                continue;
            }
            lineStart = false;

            if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
            {
                while (i < source.Length && source[i] != '\n') i++;
                continue;
            }
            if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/'))
                {
                    if (source[i] == '\n') line++;
                    i++;
                }
                i += 2;
                continue;
            }

            if (c == 'R' && i + 1 < source.Length && source[i + 1] == '"')
            {
                int startLine = line;
                int open = source.IndexOf('(', i + 2);
                string delim = ")" + source[(i + 2)..open] + "\"";
                int close = source.IndexOf(delim, open, StringComparison.Ordinal);
                int end = close < 0 ? source.Length : close + delim.Length;
                string text = source[i..end];
                line += text.Count(ch => ch == '\n');
                tokens.Add(new Token(TokenKind.String, text, startLine));
                i = end;
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_')) i++;
                // String-literal prefixes (u8"", L"") belong to the literal that follows.
                if (i < source.Length && (source[i] == '"' || source[i] == '\'') && source[start..i] is "u8" or "u" or "U" or "L")
                {
                    tokens.Add(ReadQuoted(source, ref i, ref line, source[i], start));
                    continue;
                }
                tokens.Add(new Token(TokenKind.Identifier, source[start..i], line));
                continue;
            }

            if (char.IsDigit(c) || (c == '.' && i + 1 < source.Length && char.IsDigit(source[i + 1])))
            {
                int start = i;
                while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '.' || source[i] == '\'' ||
                       ((source[i] == '+' || source[i] == '-') && (source[i - 1] is 'e' or 'E' or 'p' or 'P'))))
                    i++;
                tokens.Add(new Token(TokenKind.Number, source[start..i], line));
                continue;
            }

            if (c == '"' || c == '\'')
            {
                tokens.Add(ReadQuoted(source, ref i, ref line, c, i));
                continue;
            }

            string? punct = MultiCharPunct.FirstOrDefault(p => string.CompareOrdinal(source, i, p, 0, p.Length) == 0);
            punct ??= c.ToString();
            tokens.Add(new Token(TokenKind.Punct, punct, line));
            i += punct.Length;
        }
        return tokens;
    }

    static Token ReadQuoted(string source, ref int i, ref int line, char quote, int start)
    {
        int startLine = line;
        i++;
        while (i < source.Length && source[i] != quote)
        {
            if (source[i] == '\\') i++;
            else if (source[i] == '\n') line++;
            i++;
        }
        i++;
        return new Token(quote == '"' ? TokenKind.String : TokenKind.Char, source[start..Math.Min(i, source.Length)], startLine);
    }

    // Spaces only where two word-like tokens would otherwise fuse, so "const Vec3 &" becomes "const Vec3&".
    public static string Join(IEnumerable<Token> tokens)
    {
        var sb = new StringBuilder();
        Token? prev = null;
        foreach (var t in tokens)
        {
            if (prev != null && IsWordLike(prev) && IsWordLike(t)) sb.Append(' ');
            sb.Append(t.Text);
            prev = t;
        }
        return sb.ToString();
    }

    static bool IsWordLike(Token t) => t.Kind is TokenKind.Identifier or TokenKind.Number;
}
