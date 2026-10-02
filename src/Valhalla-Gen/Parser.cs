namespace Valhalla.Gen;

sealed class ScanResult
{
    public List<RawType> Types { get; } = [];
    public List<RawEnum> Enums { get; } = [];
    public List<RawSpecialization> Specializations { get; } = [];
}

// Text-level scanner: tracks namespaces and braces, and only looks closely at what follows a VALHALLA_ annotation.
sealed class Parser
{
    enum ScopeKind : byte { Namespace, AnonymousNamespace, Transparent, Other }
    sealed record Scope(ScopeKind Kind, string[] Names);

    readonly List<Token> m_Tokens;
    readonly string m_File;
    readonly ModuleConfig m_Module;
    readonly ScanResult m_Result;
    readonly List<Scope> m_Scopes = [];
    int m_Pos;

    Parser(List<Token> tokens, string file, ModuleConfig module, ScanResult result)
    {
        m_Tokens = tokens;
        m_File = file;
        m_Module = module;
        m_Result = result;
    }

    public static readonly string[] AnnotationMarkers = ["VALHALLA_TYPE", "VALHALLA_ENUM", "VALHALLA_SPECIALIZE"];

    // Returns false when the file has no top-level annotation at all.
    public static bool ParseFile(string file, ModuleConfig module, ScanResult result)
    {
        string text = File.ReadAllText(file);
        if (!AnnotationMarkers.Any(m => text.Contains(m, StringComparison.Ordinal))) return false;
        new Parser(Lexer.Tokenize(text), file, module, result).Run();
        return true;
    }

    Token? Peek(int offset = 0) => m_Pos + offset < m_Tokens.Count ? m_Tokens[m_Pos + offset] : null;
    int CurrentLine => Peek()?.Line ?? (m_Tokens.Count > 0 ? m_Tokens[^1].Line : 1);

    string[] CurrentNamespace => m_Scopes.Where(s => s.Kind == ScopeKind.Namespace).SelectMany(s => s.Names).ToArray();

    bool AtNamespaceScope(out string reason)
    {
        reason = "";
        if (m_Scopes.Any(s => s.Kind == ScopeKind.Other)) { reason = "must appear at namespace scope"; return false; }
        if (m_Scopes.Any(s => s.Kind == ScopeKind.AnonymousNamespace)) { reason = "cannot be reflected from an anonymous namespace"; return false; }
        return true;
    }

    void Run()
    {
        while (m_Pos < m_Tokens.Count)
        {
            var tok = m_Tokens[m_Pos];
            switch (tok.Text)
            {
                case "namespace": ParseNamespace(); break;
                case "extern" when Peek(1)?.Kind == TokenKind.String && Peek(2)?.Is("{") == true:
                    m_Scopes.Add(new Scope(ScopeKind.Transparent, []));
                    m_Pos += 3;
                    break;
                case "{": m_Scopes.Add(new Scope(ScopeKind.Other, [])); m_Pos++; break;
                case "}":
                    if (m_Scopes.Count > 0) m_Scopes.RemoveAt(m_Scopes.Count - 1);
                    m_Pos++;
                    break;
                case "VALHALLA_TYPE": ParseType(); break;
                case "VALHALLA_ENUM": ParseEnum(); break;
                case "VALHALLA_SPECIALIZE": ParseSpecialize(); break;
                case "VALHALLA_FIELD" or "VALHALLA_METHOD" or "VALHALLA_OPERATOR" or "VALHALLA_CONSTRUCTOR" or "VALHALLA_LAYOUT" or "VALHALLA_ENUM_VAL":
                    Diag.Error(m_File, tok.Line, $"{tok.Text} must appear inside a VALHALLA_TYPE/VALHALLA_ENUM body.");
                    m_Pos++;
                    break;
                default: m_Pos++; break;
            }
        }
    }

    void ParseNamespace()
    {
        m_Pos++;
        var names = new List<string>();
        while (Peek() is { } t && !t.Is("{") && !t.Is("=") && !t.Is(";"))
        {
            if (t.IsIdent && t.Text != "inline") names.Add(t.Text);
            m_Pos++;
        }
        if (Peek()?.Is("{") == true)
        {
            m_Scopes.Add(new Scope(names.Count == 0 ? ScopeKind.AnonymousNamespace : ScopeKind.Namespace, names.ToArray()));
            m_Pos++;
        }
    }

    // Returns the tokens between the macro's parentheses; leaves m_Pos after the closing ')'.
    List<Token> ReadMacroArgs()
    {
        var macro = m_Tokens[m_Pos++];
        if (Peek()?.Is("(") != true)
        {
            Diag.Error(m_File, macro.Line, $"{macro.Text} must be followed by '('.");
            return [];
        }
        int close = FindMatching(m_Pos);
        var inner = m_Tokens.GetRange(m_Pos + 1, close - m_Pos - 1);
        m_Pos = close + 1;
        return inner;
    }

    AnnotationArgs ParseAnnotation()
    {
        int line = m_Tokens[m_Pos].Line;
        var inner = ReadMacroArgs();
        var args = new AnnotationArgs { File = m_File, Line = line };
        foreach (var part in SplitTopLevel(inner, ","))
        {
            if (part.Count == 0) continue;
            int eq = part.FindIndex(t => t.Is("="));
            if (eq != 1 || !part[0].IsIdent)
            {
                Diag.Error(m_File, line, $"Malformed specifier '{Lexer.Join(part)}'; expected Key=Value.");
                continue;
            }
            args.Set(part[0].Text, part.GetRange(2, part.Count - 2));
        }
        return args;
    }

    int FindMatching(int openIndex)
    {
        string open = m_Tokens[openIndex].Text;
        string close = open switch { "(" => ")", "{" => "}", "[" => "]", "<" => ">", _ => throw new InvalidOperationException(open) };
        int depth = 0;
        for (int i = openIndex; i < m_Tokens.Count; i++)
        {
            if (m_Tokens[i].Is(open)) depth++;
            else if (m_Tokens[i].Is(close) && --depth == 0) return i;
        }
        Diag.Error(m_File, m_Tokens[openIndex].Line, $"Unbalanced '{open}'.");
        return m_Tokens.Count - 1;
    }

    // Splits on a separator at bracket depth zero; '<'/'>' count as brackets so template argument lists stay whole.
    public static List<List<Token>> SplitTopLevel(List<Token> tokens, string separator)
    {
        var parts = new List<List<Token>> { new() };
        int depth = 0, angle = 0;
        foreach (var t in tokens)
        {
            switch (t.Text)
            {
                case "(" or "[" or "{": depth++; break;
                case ")" or "]" or "}": depth--; break;
                case "<": angle++; break;
                case ">" when angle > 0: angle--; break;
            }
            if (depth == 0 && angle == 0 && t.Is(separator)) { parts.Add([]); continue; }
            parts[^1].Add(t);
        }
        if (parts.Count == 1 && parts[0].Count == 0) parts.Clear();
        return parts;
    }

    void ParseType()
    {
        int line = CurrentLine;
        var args = ParseAnnotation();
        AnnotationArgs? layout = null;
        List<Token>? templateParams = null;

        if (Peek()?.Is("VALHALLA_LAYOUT") == true) layout = ParseAnnotation();
        if (Peek()?.Is("template") == true)
        {
            m_Pos++;
            if (Peek()?.Is("<") != true) { Diag.Error(m_File, CurrentLine, "Expected '<' after template."); return; }
            int close = FindMatching(m_Pos);
            templateParams = m_Tokens.GetRange(m_Pos + 1, close - m_Pos - 1);
            m_Pos = close + 1;
        }
        if (layout == null && Peek()?.Is("VALHALLA_LAYOUT") == true) layout = ParseAnnotation();

        if (Peek() is not { } keyword || (keyword.Text != "struct" && keyword.Text != "class"))
        {
            Diag.Error(m_File, line, "VALHALLA_TYPE must be followed by a struct declaration.");
            return;
        }
        m_Pos++;

        string? name = null;
        while (Peek() is { } t && !t.Is(":") && !t.Is("{") && !t.Is(";"))
        {
            if (t.Is("alignas") || t.Is("__declspec")) { m_Pos++; if (Peek()?.Is("(") == true) m_Pos = FindMatching(m_Pos) + 1; continue; }
            if (t.Is("[")) { m_Pos = FindMatching(m_Pos) + 1; continue; }
            if (t.IsIdent && t.Text != "final") name = t.Text;
            m_Pos++;
        }
        if (name == null || Peek()?.Is(";") == true)
        {
            Diag.Error(m_File, line, "VALHALLA_TYPE must annotate a struct definition, not a forward declaration.");
            return;
        }

        var bases = new List<string>();
        if (Peek()?.Is(":") == true)
        {
            m_Pos++;
            var baseTokens = new List<Token>();
            while (Peek() is { } t && !t.Is("{")) { baseTokens.Add(t); m_Pos++; }
            foreach (var part in SplitTopLevel(baseTokens, ","))
            {
                var cleaned = part.Where(t => t.Text is not ("public" or "protected" or "private" or "virtual")).ToList();
                if (cleaned.Count > 0) bases.Add(Lexer.Join(cleaned));
            }
        }

        if (!AtNamespaceScope(out string reason))
            Diag.Error(m_File, line, $"VALHALLA_TYPE {name} {reason}.");

        int bodyOpen = m_Pos;
        int bodyClose = FindMatching(bodyOpen);
        var members = ParseBody(bodyOpen + 1, bodyClose, name);
        m_Pos = bodyClose + 1;

        var paramNames = new List<string>();
        if (templateParams != null)
        {
            foreach (var p in SplitTopLevel(templateParams, ","))
            {
                if (p.Any(t => t.Is("..."))) Diag.Error(m_File, line, $"Variadic template parameters are not reflectable ({name}).");
                int eq = p.FindIndex(t => t.Is("="));
                var decl = eq >= 0 ? p.GetRange(0, eq) : p;
                var last = decl.LastOrDefault(t => t.IsIdent);
                if (last == null) Diag.Error(m_File, line, $"Unnamed template parameter in {name}.");
                else paramNames.Add(last.Text);
            }
        }

        m_Result.Types.Add(new RawType
        {
            Name = name,
            Namespace = CurrentNamespace,
            Args = args,
            Layout = layout,
            BaseNames = bases,
            TemplateParams = templateParams,
            TemplateParamNames = paramNames,
            Members = members,
            Header = m_File,
            Line = line,
            Module = m_Module,
        });
    }

    List<RawMember> ParseBody(int start, int end, string typeName)
    {
        var members = new List<RawMember>();
        AnnotationArgs? pendingLayout = null;
        int i = start;
        while (i < end)
        {
            var t = m_Tokens[i];
            if (t.Is("{") || t.Is("(") || t.Is("["))
            {
                i = FindMatching(i) + 1;
                continue;
            }

            if (t.Text is "VALHALLA_TYPE" or "VALHALLA_ENUM")
            {
                Diag.Error(m_File, t.Line, $"Nested reflected declarations inside {typeName} are not supported.");
                i++;
                continue;
            }

            if (t.Text is not ("VALHALLA_LAYOUT" or "VALHALLA_FIELD" or "VALHALLA_METHOD" or "VALHALLA_OPERATOR" or "VALHALLA_CONSTRUCTOR"))
            {
                i++;
                continue;
            }

            m_Pos = i;
            var args = ParseAnnotation();
            i = m_Pos;

            if (t.Is("VALHALLA_LAYOUT"))
            {
                pendingLayout = args;
                continue;
            }

            var kind = t.Text switch
            {
                "VALHALLA_FIELD" => MemberKind.Field,
                "VALHALLA_METHOD" => MemberKind.Method,
                "VALHALLA_OPERATOR" => MemberKind.Operator,
                _ => MemberKind.Constructor,
            };

            List<Token> decl = [];
            if (kind != MemberKind.Constructor || !args.Has("Default"))
            {
                // A per-field VALHALLA_LAYOUT may also sit between the annotation and the declaration.
                if (i < end && m_Tokens[i].Is("VALHALLA_LAYOUT"))
                {
                    m_Pos = i;
                    pendingLayout = ParseAnnotation();
                    i = m_Pos;
                }
                (decl, i) = CollectDeclaration(i, end, kind);
            }

            if (pendingLayout != null && kind != MemberKind.Field)
                Diag.Error(m_File, t.Line, "VALHALLA_LAYOUT only applies to types and fields.");

            members.Add(new RawMember { Kind = kind, Args = args, Layout = kind == MemberKind.Field ? pendingLayout : null, Declaration = decl, Line = t.Line });
            pendingLayout = null;
        }
        if (pendingLayout != null) Diag.Error(m_File, pendingLayout.Line, "VALHALLA_LAYOUT is not followed by a field.");
        return members;
    }

    (List<Token> Decl, int Next) CollectDeclaration(int i, int end, MemberKind kind)
    {
        var decl = new List<Token>();
        bool seenParams = false;
        while (i < end)
        {
            var t = m_Tokens[i];
            if (t.Is(";")) return (decl, i + 1);

            if (kind != MemberKind.Field && seenParams && t.Is("{"))
                return (decl, FindMatching(i) + 1);

            if (kind == MemberKind.Constructor && seenParams && t.Is(":"))
            {
                // In an init list, '{' after a name (or a template-id's '>') is a brace initialiser; any other '{' opens the body.
                int j = i + 1;
                while (j < end)
                {
                    var tk = m_Tokens[j];
                    if (tk.Is("(")) { j = FindMatching(j) + 1; continue; }
                    if (tk.Is("{"))
                    {
                        var prev = m_Tokens[j - 1];
                        if (prev.IsIdent || prev.Is(">")) { j = FindMatching(j) + 1; continue; }
                        return (decl, FindMatching(j) + 1);
                    }
                    j++;
                }
                Diag.Error(m_File, t.Line, "Constructor initialiser list has no body.");
                return (decl, end);
            }

            if (t.Is("(") || t.Is("[") || t.Is("{"))
            {
                int close = FindMatching(i);
                decl.AddRange(m_Tokens.GetRange(i, close - i + 1));
                if (t.Is("(")) seenParams = true;
                i = close + 1;
                continue;
            }

            decl.Add(t);
            i++;
        }
        Diag.Error(m_File, m_Tokens[Math.Min(i, m_Tokens.Count - 1)].Line, "Unterminated annotated declaration.");
        return (decl, end);
    }

    void ParseEnum()
    {
        int line = CurrentLine;
        var args = ParseAnnotation();
        if (Peek()?.Is("enum") != true)
        {
            Diag.Error(m_File, line, "VALHALLA_ENUM must be followed by an enum class declaration.");
            return;
        }
        m_Pos++;
        if (Peek() is not { } scoped || (scoped.Text != "class" && scoped.Text != "struct"))
        {
            Diag.Error(m_File, line, "VALHALLA_ENUM requires an enum class.");
            return;
        }
        m_Pos++;
        if (Peek() is not { IsIdent: true } nameTok)
        {
            Diag.Error(m_File, line, "Expected enum name.");
            return;
        }
        m_Pos++;

        string? underlying = null;
        if (Peek()?.Is(":") == true)
        {
            m_Pos++;
            var u = new List<Token>();
            while (Peek() is { } t && !t.Is("{") && !t.Is(";")) { u.Add(t); m_Pos++; }
            underlying = Lexer.Join(u);
        }
        if (Peek()?.Is("{") != true)
        {
            Diag.Error(m_File, line, $"VALHALLA_ENUM {nameTok.Text} must annotate a definition.");
            return;
        }

        if (!AtNamespaceScope(out string reason))
            Diag.Error(m_File, line, $"VALHALLA_ENUM {nameTok.Text} {reason}.");

        int close = FindMatching(m_Pos);
        var body = m_Tokens.GetRange(m_Pos + 1, close - m_Pos - 1);
        m_Pos = close + 1;

        var values = new List<RawEnumValue>();
        foreach (var entry in SplitTopLevel(body, ","))
        {
            if (entry.Count == 0) continue;
            AnnotationArgs? valueArgs = null;
            int idx = 0;
            if (entry[0].Is("VALHALLA_ENUM_VAL"))
            {
                var sub = new Parser(entry, m_File, m_Module, m_Result);
                valueArgs = sub.ParseAnnotation();
                idx = sub.m_Pos;
            }
            if (idx >= entry.Count || !entry[idx].IsIdent)
            {
                Diag.Error(m_File, entry[0].Line, "Malformed enum entry.");
                continue;
            }
            values.Add(new RawEnumValue { Name = entry[idx].Text, Args = valueArgs, Line = entry[idx].Line });
        }

        m_Result.Enums.Add(new RawEnum
        {
            Name = nameTok.Text,
            Namespace = CurrentNamespace,
            Args = args,
            Underlying = underlying,
            Values = values,
            Header = m_File,
            Line = line,
            Module = m_Module,
        });
    }

    void ParseSpecialize()
    {
        int line = CurrentLine;
        var inner = ReadMacroArgs();
        if (inner.Count == 0)
        {
            Diag.Error(m_File, line, "VALHALLA_SPECIALIZE requires a template-id, e.g. VALHALLA_SPECIALIZE(RArray<float>).");
            return;
        }
        m_Result.Specializations.Add(new RawSpecialization
        {
            Tokens = inner,
            Namespace = CurrentNamespace,
            Header = m_File,
            Line = line,
            Module = m_Module,
        });
    }
}
