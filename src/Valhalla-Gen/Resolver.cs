namespace Valhalla.Gen;

sealed class Inventory
{
    public List<TypeModel> Types { get; } = [];
    public List<EnumModel> Enums { get; } = [];
    public Dictionary<string, TypeModel> TypesByName { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, EnumModel> EnumsByName { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, RawType> TemplatesByName { get; } = new(StringComparer.Ordinal);
    // Specialisations are emitted by the module that wrote VALHALLA_SPECIALIZE, not the template's module.
    public Dictionary<TypeModel, ModuleConfig> SpecializationOwners { get; } = [];

    public ModuleConfig OwnerOf(TypeModel type) => SpecializationOwners.TryGetValue(type, out var m) ? m : type.Module;
}

sealed class Resolver
{
    static readonly Dictionary<string, (string Kind, string Symbol)> Operators = new()
    {
        ["Plus"] = ("Add", "+"), ["Minus"] = ("Sub", "-"), ["Multiply"] = ("Mul", "*"), ["Divide"] = ("Div", "/"), ["Modulo"] = ("Mod", "%"),
        ["PlusAssign"] = ("AddAssign", "+="), ["MinusAssign"] = ("SubAssign", "-="), ["MultiplyAssign"] = ("MulAssign", "*="),
        ["DivideAssign"] = ("DivAssign", "/="), ["ModuloAssign"] = ("ModAssign", "%="),
        ["Equals"] = ("Equal", "=="), ["NotEquals"] = ("NotEqual", "!="), ["Less"] = ("Less", "<"), ["Greater"] = ("Greater", ">"),
        ["LessEquals"] = ("LessEqual", "<="), ["GreaterEquals"] = ("GreaterEqual", ">="), ["Spaceship"] = ("Spaceship", "<=>"),
        ["And"] = ("LogicalAnd", "&&"), ["Or"] = ("LogicalOr", "||"), ["Not"] = ("LogicalNot", "!"),
        ["BitAnd"] = ("BitAnd", "&"), ["BitOr"] = ("BitOr", "|"), ["BitXor"] = ("BitXor", "^"), ["BitNot"] = ("BitNot", "~"),
        ["ShiftLeft"] = ("ShiftLeft", "<<"), ["ShiftRight"] = ("ShiftRight", ">>"),
        ["BitAndAssign"] = ("BitAndAssign", "&="), ["BitOrAssign"] = ("BitOrAssign", "|="), ["BitXorAssign"] = ("BitXorAssign", "^="),
        ["ShiftLeftAssign"] = ("ShiftLeftAssign", "<<="), ["ShiftRightAssign"] = ("ShiftRightAssign", ">>="),
        ["Assign"] = ("Assign", "="), ["Subscript"] = ("Subscript", "[]"), ["Call"] = ("Call", "()"), ["Arrow"] = ("Arrow", "->"),
        ["Cast"] = ("Cast", ""), ["Increment"] = ("Increment", "++"), ["Decrement"] = ("Decrement", "--"),
    };

    static readonly HashSet<string> FieldLangTraits = ["Static", "Const"];
    static readonly HashSet<string> FieldReflectTraits = ["Pointer", "Reference"];
    static readonly HashSet<string> FieldToolTraits = ["Deprecated"];
    static readonly HashSet<string> MethodLangTraits = ["Static", "Const", "Override", "Overridden", "Abstract", "Final"];
    static readonly HashSet<string> MethodReflectTraits = ["Functor"];
    static readonly HashSet<string> MethodToolTraits = ["Discardable", "Deprecated"];
    static readonly HashSet<string> TypeKeywords =
        ["int", "char", "short", "long", "unsigned", "signed", "float", "double", "bool", "void", "const", "volatile", "auto"];

    readonly Inventory m_Inv = new();
    readonly PrimitiveTable m_Primitives;

    Resolver(PrimitiveTable primitives) => m_Primitives = primitives;

    public static Inventory Resolve(ScanResult scan, PrimitiveTable primitives)
    {
        var r = new Resolver(primitives);
        r.Register(scan);
        r.ExpandSpecializations(scan);
        foreach (var type in r.m_Inv.Types.ToList()) r.Materialize(type);
        foreach (var e in r.m_Inv.Enums) r.MaterializeEnum(e);
        foreach (var type in r.m_Inv.Types) r.ResolveParent(type);
        foreach (var type in r.m_Inv.Types) r.ValidateOverrides(type);
        return r.m_Inv;
    }

    void Register(ScanResult scan)
    {
        foreach (var raw in scan.Types)
        {
            string q = raw.QualifiedName;
            if (m_Inv.TypesByName.ContainsKey(q) || m_Inv.TemplatesByName.ContainsKey(q) || m_Inv.EnumsByName.ContainsKey(q))
            {
                Diag.Error(raw.Header, raw.Line, $"Reflected type '{q}' is declared more than once.");
                continue;
            }
            if (raw.IsTemplate)
            {
                m_Inv.TemplatesByName[q] = raw;
                continue;
            }
            var model = new TypeModel { Name = raw.Name, Namespace = raw.Namespace, Source = raw };
            m_Inv.Types.Add(model);
            m_Inv.TypesByName[q] = model;
        }

        foreach (var raw in scan.Enums)
        {
            string q = raw.QualifiedName;
            if (m_Inv.TypesByName.ContainsKey(q) || m_Inv.TemplatesByName.ContainsKey(q) || m_Inv.EnumsByName.ContainsKey(q))
            {
                Diag.Error(raw.Header, raw.Line, $"Reflected type '{q}' is declared more than once.");
                continue;
            }
            var model = new EnumModel { Source = raw };
            m_Inv.Enums.Add(model);
            m_Inv.EnumsByName[q] = model;
        }
    }


    void ExpandSpecializations(ScanResult scan)
    {
        foreach (var spec in scan.Specializations)
        {
            int lt = spec.Tokens.FindIndex(t => t.Is("<"));
            if (lt <= 0 || !spec.Tokens[^1].Is(">"))
            {
                Diag.Error(spec.Header, spec.Line, $"VALHALLA_SPECIALIZE expects a template-id, got '{Lexer.Join(spec.Tokens)}'.");
                continue;
            }

            string templateSpelling = Lexer.Join(spec.Tokens.GetRange(0, lt)).TrimStart(':');
            var template = LookupScoped(spec.Namespace, templateSpelling, m_Inv.TemplatesByName);
            if (template == null)
            {
                Diag.Error(spec.Header, spec.Line, $"VALHALLA_SPECIALIZE target '{templateSpelling}' is not a VALHALLA_TYPE template.");
                continue;
            }

            var argParts = Parser.SplitTopLevel(spec.Tokens.GetRange(lt + 1, spec.Tokens.Count - lt - 2), ",");
            if (argParts.Count != template.TemplateParamNames.Count)
            {
                Diag.Error(spec.Header, spec.Line, $"'{template.QualifiedName}' takes {template.TemplateParamNames.Count} template argument(s), got {argParts.Count}.");
                continue;
            }

            var canonical = new List<string>();
            var emitted = new List<string>();
            foreach (var arg in argParts)
            {
                var (canon, emit) = CanonicalizeArgument(arg, spec.Namespace, spec.Module, spec.Header, spec.Line);
                canonical.Add(canon);
                emitted.Add(emit);
            }

            string name = $"{template.Name}<{string.Join(", ", canonical)}>";
            string qualified = Model.Qualify(template.Namespace, name);
            if (m_Inv.TypesByName.ContainsKey(qualified))
            {
                Diag.Warning(spec.Header, spec.Line, $"'{qualified}' is specialised more than once; ignoring the duplicate.");
                continue;
            }

            var model = new TypeModel
            {
                Name = name,
                Namespace = template.Namespace,
                Source = template,
                SpecializationArgsEmit = string.Join(", ", emitted),
            };
            m_Inv.Types.Add(model);
            m_Inv.TypesByName[qualified] = model;
            if (spec.Module != template.Module) m_Inv.SpecializationOwners[model] = spec.Module;
        }
    }

    (string Canonical, string Emit) CanonicalizeArgument(List<Token> arg, string[] ns, ModuleConfig module, string file, int line)
    {
        string text = Lexer.Join(arg);
        if (arg.Count > 0 && arg.All(t => t.Kind == TokenKind.Number || t.Is("-"))) return (text, text);

        var stripped = StripQualifiers(arg, out bool isConst, out string suffix);
        var baseRef = ResolveBase(stripped, ns, module, file, line, reportErrors: false);
        if (baseRef == null) return (text, text);

        string canonBase = baseRef.Kind switch
        {
            TypeRefKind.Primitive => baseRef.Primitive,
            TypeRefKind.Reflected => baseRef.Type!.QualifiedName,
            TypeRefKind.Enum => baseRef.Enum!.QualifiedName,
            _ => "void",
        };
        string emitBase = baseRef.Kind switch
        {
            TypeRefKind.Reflected => baseRef.Type!.FullEmitName,
            TypeRefKind.Enum => baseRef.Enum!.FullEmitName,
            _ => canonBase,
        };
        string prefix = isConst ? "const " : "";
        return (prefix + canonBase + suffix, prefix + emitBase + suffix);
    }


    static List<Token> StripQualifiers(List<Token> tokens, out bool isConst, out string suffix)
    {
        isConst = false;
        var list = new List<Token>(tokens);
        while (list.Count > 0 && list[0].Text is "const" or "volatile" or "typename" or "struct" or "class" or "enum")
        {
            if (list[0].Is("const")) isConst = true;
            list.RemoveAt(0);
        }
        var suffixTokens = new List<Token>();
        while (list.Count > 0 && list[^1].Text is "*" or "&" or "&&" or "const" or "volatile")
        {
            suffixTokens.Insert(0, list[^1]);
            list.RemoveAt(list.Count - 1);
        }
        // "int const" spelling: trailing const directly on the base binds to it, not to a pointer.
        if (suffixTokens.Count > 0 && suffixTokens[0].Is("const"))
        {
            isConst = true;
            suffixTokens.RemoveAt(0);
        }
        suffix = string.Concat(suffixTokens.Select(t => t.Text));
        return list;
    }

    TypeRef? ResolveType(List<Token> tokens, string[] ns, ModuleConfig module, string file, int line)
    {
        var baseTokens = StripQualifiers(tokens, out bool isConst, out string suffix);
        if (baseTokens.Count == 1 && baseTokens[0].Is("void"))
        {
            if (suffix.Length == 0) return new TypeRef { Kind = TypeRefKind.Void };
            Diag.Error(file, line, "void* is not reflectable; use a reflected or primitive pointee type.");
            return null;
        }
        var result = ResolveBase(baseTokens, ns, module, file, line, reportErrors: true);
        if (result == null) return null;
        result.IsConst = isConst;
        result.IsPointer = suffix.Contains('*');
        result.IsReference = suffix.Contains('&');
        return result;
    }

    TypeRef? ResolveBase(List<Token> baseTokens, string[] ns, ModuleConfig module, string file, int line, bool reportErrors)
    {
        string spelling = Lexer.Join(baseTokens);
        if (spelling.StartsWith("::")) spelling = spelling[2..];

        if (m_Primitives.Normalize(spelling) is { } prim)
            return new TypeRef { Kind = TypeRefKind.Primitive, Primitive = prim };

        TypeModel? type = null;
        EnumModel? enumModel = null;

        int lt = baseTokens.FindIndex(t => t.Is("<"));
        if (lt > 0 && baseTokens[^1].Is(">"))
        {
            string templateSpelling = Lexer.Join(baseTokens.GetRange(0, lt)).TrimStart(':');
            var template = LookupScoped(ns, templateSpelling, m_Inv.TemplatesByName);
            if (template != null)
            {
                var args = Parser.SplitTopLevel(baseTokens.GetRange(lt + 1, baseTokens.Count - lt - 2), ",")
                    .Select(a => CanonicalizeArgument(a, ns, module, file, line).Canonical);
                string qualified = Model.Qualify(template.Namespace, $"{template.Name}<{string.Join(", ", args)}>");
                m_Inv.TypesByName.TryGetValue(qualified, out type);
                if (type == null && reportErrors)
                {
                    Diag.Error(file, line, $"'{qualified}' is used but never declared with VALHALLA_SPECIALIZE.");
                    return null;
                }
            }
        }
        else
        {
            type = LookupScoped(ns, spelling, m_Inv.TypesByName);
            if (type == null) enumModel = LookupScoped(ns, spelling, m_Inv.EnumsByName);
        }

        if (type == null && enumModel == null)
        {
            if (reportErrors)
                Diag.Error(file, line, $"Type '{spelling}' is neither a Valhalla primitive nor a VALHALLA_TYPE/VALHALLA_ENUM.");
            return null;
        }

        var owner = type != null ? m_Inv.OwnerOf(type) : enumModel!.Module;
        if (owner != module && !module.Dependencies.Contains(owner.ModuleName))
        {
            if (reportErrors)
                Diag.Error(file, line, $"'{(type?.QualifiedName ?? enumModel!.QualifiedName)}' belongs to module '{owner.ModuleName}', which is not listed in {module.ModuleName}'s Dependencies.");
            return null;
        }

        return type != null
            ? new TypeRef { Kind = TypeRefKind.Reflected, Type = type }
            : new TypeRef { Kind = TypeRefKind.Enum, Enum = enumModel };
    }

    // C++-style lookup: innermost enclosing namespace first, then outward to global.
    static T? LookupScoped<T>(string[] ns, string spelling, Dictionary<string, T> table) where T : class
    {
        for (int k = ns.Length; k >= 0; k--)
        {
            if (table.TryGetValue(Model.Qualify(ns[..k], spelling), out var found)) return found;
        }
        return null;
    }


    void Materialize(TypeModel type)
    {
        var raw = type.Source;
        var args = raw.Args;
        string file = raw.Header;

        foreach (string key in args.Keys)
        {
            if (key is not ("define" or "Parent" or "DisplayName" or "Category" or "Tooltip"))
                Diag.Error(file, raw.Line, $"Unknown VALHALLA_TYPE specifier '{key}'.");
        }

        string define = args.Symbol("define") ?? "ObjectType.Struct";
        if (define is not ("ObjectType.Class" or "ObjectType.Struct"))
            Diag.Error(file, raw.Line, $"define must be ObjectType.Class or ObjectType.Struct, got '{define}'.");
        type.IsClass = define == "ObjectType.Class";
        type.Meta = ReadMeta(args, type.Name);
        type.Align = ReadAlign(raw.Layout, file, raw.Line);
        type.ParentSpelling = args.Symbol("Parent");

        var substitution = BuildSubstitution(type);
        var ns = raw.Namespace;
        var module = m_Inv.OwnerOf(type);

        foreach (var member in raw.Members)
        {
            var decl = Substitute(member.Declaration, substitution);
            switch (member.Kind)
            {
                case MemberKind.Field:
                    if (MaterializeField(type, member, decl, ns, module) is { } f) type.Fields.Add(f);
                    break;
                case MemberKind.Method:
                case MemberKind.Operator:
                    if (MaterializeMethod(type, member, decl, ns, module) is { } m)
                        (member.Kind == MemberKind.Method ? type.Methods : type.Operators).Add(m);
                    break;
                case MemberKind.Constructor:
                    if (MaterializeConstructor(type, member, decl, ns, module) is { } c) type.Constructors.Add(c);
                    break;
            }
        }

        AssignIdents(type.Methods, m => m.Name);
        AssignIdents(type.Operators, m => m.Operator == "Cast" ? "Cast" : m.Operator!);
        AssignIdents(type.Constructors, c => c.Kind);
    }

    static void AssignIdents<T>(List<T> items, Func<T, string> baseName)
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            string b = Model.Identifier(baseName(item));
            int n = seen.TryGetValue(b, out int c) ? c : 0;
            seen[b] = n + 1;
            string ident = n == 0 ? b : $"{b}_{n}";
            switch (item)
            {
                case MethodModel m: m.Ident = ident; break;
                case ConstructorModel ctor: ctor.Ident = ident; break;
            }
        }
    }

    Dictionary<string, List<Token>> BuildSubstitution(TypeModel type)
    {
        var map = new Dictionary<string, List<Token>>(StringComparer.Ordinal);
        if (type.SpecializationArgsEmit == null) return map;
        var args = Parser.SplitTopLevel(Lexer.Tokenize(type.SpecializationArgsEmit), ",");
        for (int i = 0; i < type.Source.TemplateParamNames.Count && i < args.Count; i++)
            map[type.Source.TemplateParamNames[i]] = args[i];
        return map;
    }

    static List<Token> Substitute(List<Token> tokens, Dictionary<string, List<Token>> map)
    {
        if (map.Count == 0) return tokens;
        var result = new List<Token>(tokens.Count);
        for (int i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            bool isMemberAccess = i > 0 && (tokens[i - 1].Is("::") || tokens[i - 1].Is(".") || tokens[i - 1].Is("->"));
            if (t.IsIdent && !isMemberAccess && map.TryGetValue(t.Text, out var replacement))
                result.AddRange(replacement.Select(r => r with { Line = t.Line }));
            else
                result.Add(t);
        }
        return result;
    }

    static MetaStrings ReadMeta(AnnotationArgs args, string fallbackName) => new()
    {
        DisplayName = args.String("DisplayName") ?? Model.Literal(fallbackName),
        Category = args.String("Category") ?? "\"\"",
        Tooltip = args.String("Tooltip") ?? "\"\"",
    };

    static int? ReadAlign(AnnotationArgs? layout, string file, int line)
    {
        if (layout == null) return null;
        foreach (string key in layout.Keys)
            if (key != "Align") Diag.Error(file, line, $"Unknown VALHALLA_LAYOUT specifier '{key}'.");
        string? text = layout.Symbol("Align");
        if (text == null) return null;
        if (int.TryParse(text, out int align) && align > 0 && (align & (align - 1)) == 0) return align;
        Diag.Error(file, line, $"Align must be a positive power of two, got '{text}'.");
        return null;
    }

    string ResolveVisibility(AnnotationArgs args, TypeModel type, string file, int line)
    {
        string v = args.Symbol("Visibility") ?? "Default";
        v = v.StartsWith("Visibility.") ? v["Visibility.".Length..] : v;
        if (v == "Default") return type.DefaultVisibility;
        if (v is "Public" or "Private" or "Protected") return v;
        Diag.Error(file, line, $"Visibility must be Public, Private, Protected or Default, got '{v}'.");
        return type.DefaultVisibility;
    }

    static List<string> ReadTraits(AnnotationArgs args, string bucket, HashSet<string> allowed, string what, string file, int line)
    {
        var result = new List<string>();
        foreach (string raw in args.SymbolList(bucket))
        {
            string trait = raw.StartsWith("Behave.") ? raw["Behave.".Length..] : raw;
            if (!allowed.Contains(trait))
            {
                Diag.Error(file, line, $"Behave.{trait} is not valid in {bucket} for a {what}.");
                continue;
            }
            if (!result.Contains(trait)) result.Add(trait);
        }
        return result;
    }

    static void CheckKeys(AnnotationArgs args, string macro, string[] allowed, string file, int line)
    {
        foreach (string key in args.Keys)
            if (!allowed.Contains(key)) Diag.Error(file, line, $"Unknown {macro} specifier '{key}'.");
    }

    // Strips leading decl-specifiers and attributes, reporting the ones that matter.
    static List<Token> StripSpecifiers(List<Token> decl, out HashSet<string> found)
    {
        found = [];
        var list = new List<Token>(decl);
        while (list.Count > 0)
        {
            if (list[0].Text is "static" or "inline" or "constexpr" or "constinit" or "consteval" or "mutable" or "virtual" or "explicit" or "friend")
            {
                found.Add(list[0].Text);
                list.RemoveAt(0);
                continue;
            }
            if (list[0].Is("[") && list.Count > 1 && list[1].Is("["))
            {
                int depth = 0, i = 0;
                for (; i < list.Count; i++)
                {
                    if (list[i].Is("[")) depth++;
                    else if (list[i].Is("]") && --depth == 0) break;
                }
                list.RemoveRange(0, i + 1);
                continue;
            }
            if (list[0].Is("alignas") && list.Count > 1 && list[1].Is("("))
            {
                int depth = 0, i = 1;
                for (; i < list.Count; i++)
                {
                    if (list[i].Is("(")) depth++;
                    else if (list[i].Is(")") && --depth == 0) break;
                }
                list.RemoveRange(0, i + 1);
                continue;
            }
            break;
        }
        return list;
    }

    FieldModel? MaterializeField(TypeModel type, RawMember member, List<Token> decl, string[] ns, ModuleConfig module)
    {
        string file = type.Source.Header;
        int line = member.Line;
        var args = member.Args;
        CheckKeys(args, "VALHALLA_FIELD", ["Visibility", "LangTraits", "ReflectTraits", "ToolTraits", "DisplayName", "Category", "Tooltip"], file, line);

        var body = StripSpecifiers(decl, out var specs);
        int cut = body.FindIndex(t => t.Is("=") || t.Is("{"));
        if (cut >= 0) body = body.GetRange(0, cut);
        int bracket = body.FindIndex(t => t.Is("["));
        if (bracket >= 0) body = body.GetRange(0, bracket);
        if (body.Any(t => t.Is(":")))
        {
            Diag.Error(file, line, "Bit-field members cannot be reflected (no addressable offset).");
            return null;
        }
        if (body.Count < 2 || !body[^1].IsIdent)
        {
            Diag.Error(file, line, $"Could not parse field declaration '{Lexer.Join(decl)}'.");
            return null;
        }

        string name = body[^1].Text;
        var typeTokens = body.GetRange(0, body.Count - 1);
        var typeRef = ResolveType(typeTokens, ns, module, file, line);
        if (typeRef == null) return null;
        if (typeRef.Kind == TypeRefKind.Void)
        {
            Diag.Error(file, line, $"Field '{name}' cannot have type void.");
            return null;
        }

        var lang = ReadTraits(args, "LangTraits", FieldLangTraits, "field", file, line);
        var reflect = ReadTraits(args, "ReflectTraits", FieldReflectTraits, "field", file, line);
        var tool = ReadTraits(args, "ToolTraits", FieldToolTraits, "field", file, line);

        bool isStatic = specs.Contains("static");
        if (lang.Contains("Static") != isStatic)
            Diag.Error(file, line, isStatic
                ? $"Field '{name}' is static in C++ but is missing LangTraits={{Behave.Static}}."
                : $"Field '{name}' declares Behave.Static but is not static in C++.");

        CheckDeclaredShape(reflect, "Pointer", typeRef.IsPointer, name, file, line);
        CheckDeclaredShape(reflect, "Reference", typeRef.IsReference, name, file, line);
        if (typeRef.IsReference && !isStatic)
        {
            Diag.Error(file, line, $"Reference field '{name}' has no offset to reflect; store a pointer instead.");
            return null;
        }

        return new FieldModel
        {
            Name = name,
            TypeText = Lexer.Join(typeTokens),
            Type = typeRef,
            Meta = ReadMeta(args, name),
            Visibility = ResolveVisibility(args, type, file, line),
            LangTraits = lang,
            ReflectTraits = reflect,
            ToolTraits = tool,
            Align = ReadAlign(member.Layout, file, line),
            IsStatic = isStatic,
            Line = line,
        };
    }

    // Pointer/reference are inferred from the declaration; an explicit trait that contradicts it is an error.
    static void CheckDeclaredShape(List<string> reflect, string trait, bool actual, string name, string file, int line)
    {
        if (reflect.Contains(trait) && !actual)
            Diag.Error(file, line, $"'{name}' declares Behave.{trait} but its C++ type is not a {trait.ToLowerInvariant()}.");
        if (actual && !reflect.Contains(trait)) reflect.Add(trait);
    }

    MethodModel? MaterializeMethod(TypeModel type, RawMember member, List<Token> decl, string[] ns, ModuleConfig module)
    {
        string file = type.Source.Header;
        int line = member.Line;
        var args = member.Args;
        bool isOperator = member.Kind == MemberKind.Operator;
        string macro = isOperator ? "VALHALLA_OPERATOR" : "VALHALLA_METHOD";
        string[] allowedKeys = isOperator
            ? ["Op", "Visibility", "LangTraits", "ReflectTraits", "ToolTraits", "DisplayName", "Category", "Tooltip"]
            : ["Visibility", "LangTraits", "ReflectTraits", "ToolTraits", "DisplayName", "Category", "Tooltip"];
        CheckKeys(args, macro, allowedKeys, file, line);

        var body = StripSpecifiers(decl, out var specs);
        if (specs.Contains("friend"))
        {
            Diag.Error(file, line, "Friend functions cannot be reflected as members.");
            return null;
        }

        int nameStart, parenOpen;
        string name;
        List<Token> returnTokens;
        int opIndex = body.FindIndex(t => t.Is("operator"));

        if (isOperator || opIndex >= 0)
        {
            if (opIndex < 0)
            {
                Diag.Error(file, line, "VALHALLA_OPERATOR must annotate an operator declaration.");
                return null;
            }
            nameStart = opIndex;
            int p = opIndex + 1;
            if (p + 1 < body.Count && body[p].Is("(") && body[p + 1].Is(")")) p += 2;
            else if (p + 1 < body.Count && body[p].Is("[") && body[p + 1].Is("]")) p += 2;
            else while (p < body.Count && !body[p].Is("(")) p++;
            parenOpen = p;
            var symbolTokens = body.GetRange(opIndex + 1, parenOpen - opIndex - 1);
            bool isCast = symbolTokens.Count > 0 && symbolTokens.Any(t => t.IsIdent);
            name = isCast ? "operator " + Lexer.Join(symbolTokens) : "operator" + string.Concat(symbolTokens.Select(t => t.Text));
            returnTokens = isCast ? symbolTokens : body.GetRange(0, opIndex);
        }
        else
        {
            parenOpen = body.FindIndex(t => t.Is("("));
            if (parenOpen <= 0 || !body[parenOpen - 1].IsIdent)
            {
                Diag.Error(file, line, $"Could not parse method declaration '{Lexer.Join(decl)}'.");
                return null;
            }
            nameStart = parenOpen - 1;
            name = body[nameStart].Text;
            returnTokens = body.GetRange(0, nameStart);
        }

        if (parenOpen >= body.Count)
        {
            Diag.Error(file, line, $"Could not find the parameter list of '{name}'.");
            return null;
        }
        int parenClose = MatchParen(body, parenOpen);
        var paramTokens = body.GetRange(parenOpen + 1, parenClose - parenOpen - 1);
        var tail = body.GetRange(parenClose + 1, body.Count - parenClose - 1);

        bool isConst = false, isAbstract = false;
        for (int i = 0; i < tail.Count; i++)
        {
            var t = tail[i];
            if (t.Is("const")) isConst = true;
            else if (t.Is("&") || t.Is("&&"))
            {
                Diag.Error(file, line, $"Ref-qualified method '{name}' is not reflectable.");
                return null;
            }
            else if (t.Is("->"))
            {
                int end = tail.FindIndex(i + 1, x => x.Is("=") || x.Is("override") || x.Is("final"));
                returnTokens = tail.GetRange(i + 1, (end < 0 ? tail.Count : end) - i - 1);
                i = end < 0 ? tail.Count : end - 1;
            }
            else if (t.Is("=") && i + 1 < tail.Count && tail[i + 1].Is("0")) isAbstract = true;
            else if (t.Is("=") && i + 1 < tail.Count && tail[i + 1].Is("delete"))
            {
                Diag.Error(file, line, $"Deleted function '{name}' cannot be reflected.");
                return null;
            }
        }
        if (returnTokens.Count == 1 && returnTokens[0].Is("auto"))
        {
            Diag.Error(file, line, $"'{name}' needs an explicit (or trailing) return type to be reflected.");
            return null;
        }

        var returnType = ResolveType(returnTokens, ns, module, file, line);
        var parameters = ParseParams(paramTokens, ns, module, file, line);
        if (returnType == null || parameters == null) return null;

        string what = isOperator ? "operator" : "method";
        var lang = ReadTraits(args, "LangTraits", MethodLangTraits, what, file, line);
        var reflect = ReadTraits(args, "ReflectTraits", MethodReflectTraits, what, file, line);
        var tool = ReadTraits(args, "ToolTraits", MethodToolTraits, what, file, line);

        bool isStatic = specs.Contains("static");
        if (lang.Contains("Static") != isStatic)
            Diag.Error(file, line, isStatic ? $"'{name}' is static in C++ but is missing Behave.Static." : $"'{name}' declares Behave.Static but is not static in C++.");
        if (lang.Contains("Const") != isConst)
            Diag.Error(file, line, isConst ? $"'{name}' is const in C++ but is missing Behave.Const." : $"'{name}' declares Behave.Const but is not const in C++.");
        if (lang.Contains("Abstract") != isAbstract)
            Diag.Error(file, line, isAbstract ? $"'{name}' is pure virtual but is missing Behave.Abstract." : $"'{name}' declares Behave.Abstract but is not '= 0' in C++.");
        if (lang.Contains("Abstract") && reflect.Contains("Functor"))
            Diag.Error(file, line, $"Abstract '{name}' cannot be Behave.Functor.");

        string? op = null;
        if (isOperator)
        {
            string? opSpec = args.Symbol("Op");
            string key = opSpec != null && opSpec.StartsWith("Operator.") ? opSpec["Operator.".Length..] : opSpec ?? "";
            if (!Operators.TryGetValue(key, out var info))
            {
                Diag.Error(file, line, $"VALHALLA_OPERATOR needs Op=Operator.<Kind>; '{opSpec}' is not a known operator.");
                return null;
            }
            bool matches = info.Kind == "Cast" ? name.StartsWith("operator ") : name == "operator" + info.Symbol;
            if (!matches)
                Diag.Error(file, line, $"Op=Operator.{key} does not match the declared '{name}'.");
            op = info.Kind;
        }

        return new MethodModel
        {
            Name = name,
            ReturnText = Lexer.Join(returnTokens),
            ReturnType = returnType,
            Params = parameters,
            Meta = ReadMeta(args, name),
            Visibility = ResolveVisibility(args, type, file, line),
            LangTraits = lang,
            ReflectTraits = reflect,
            ToolTraits = tool,
            IsStatic = isStatic,
            IsConst = isConst,
            IsAbstract = isAbstract,
            Operator = op,
            Line = line,
        };
    }

    ConstructorModel? MaterializeConstructor(TypeModel type, RawMember member, List<Token> decl, string[] ns, ModuleConfig module)
    {
        string file = type.Source.Header;
        int line = member.Line;
        var args = member.Args;
        CheckKeys(args, "VALHALLA_CONSTRUCTOR", ["Visibility", "Default"], file, line);
        string visibility = ResolveVisibility(args, type, file, line);

        if (args.Symbol("Default") is { } def)
        {
            string kind = def.StartsWith("Constructor.") ? def["Constructor.".Length..] : def;
            if (kind is not ("Default" or "Copy" or "Move"))
            {
                Diag.Error(file, line, $"Default must be Constructor.Default, Constructor.Copy or Constructor.Move, got '{def}'.");
                return null;
            }
            return new ConstructorModel { Kind = kind, Visibility = visibility, Line = line };
        }

        var body = StripSpecifiers(decl, out _);
        int paren = body.FindIndex(t => t.Is("("));
        if (paren != 1 || body[0].Text != type.Source.Name)
        {
            Diag.Error(file, line, $"VALHALLA_CONSTRUCTOR without Default= must annotate a constructor of {type.Source.Name}.");
            return null;
        }
        int close = MatchParen(body, paren);
        var parameters = ParseParams(body.GetRange(paren + 1, close - paren - 1), ns, module, file, line);
        if (parameters == null) return null;
        return new ConstructorModel { Kind = "Custom", Visibility = visibility, Params = parameters, Line = line };
    }

    static int MatchParen(List<Token> tokens, int open)
    {
        int depth = 0;
        for (int i = open; i < tokens.Count; i++)
        {
            if (tokens[i].Is("(")) depth++;
            else if (tokens[i].Is(")") && --depth == 0) return i;
        }
        return tokens.Count - 1;
    }

    List<ParamModel>? ParseParams(List<Token> tokens, string[] ns, ModuleConfig module, string file, int line)
    {
        var result = new List<ParamModel>();
        var parts = Parser.SplitTopLevel(tokens, ",");
        if (parts.Count == 1 && parts[0].Count == 1 && parts[0][0].Is("void")) return result;

        bool ok = true;
        foreach (var raw in parts)
        {
            int eq = raw.FindIndex(t => t.Is("="));
            var p = eq >= 0 ? raw.GetRange(0, eq) : raw;
            if (p.Any(t => t.Is("...")))
            {
                Diag.Error(file, line, "Variadic parameters are not reflectable.");
                ok = false;
                continue;
            }

            string name = $"arg{result.Count}";
            var typeTokens = p;
            bool lastIsName = p.Count > 1 && p[^1].IsIdent && !TypeKeywords.Contains(p[^1].Text) && !p[^2].Is("::");
            if (lastIsName)
            {
                name = p[^1].Text;
                typeTokens = p.GetRange(0, p.Count - 1);
            }

            var typeRef = ResolveType(typeTokens, ns, module, file, line);
            if (typeRef == null) { ok = false; continue; }
            if (typeRef.Kind == TypeRefKind.Void)
            {
                Diag.Error(file, line, $"Parameter '{name}' cannot be void.");
                ok = false;
                continue;
            }
            result.Add(new ParamModel { Name = name, TypeText = Lexer.Join(typeTokens), Type = typeRef });
        }

        if (result.Count > 16)
            Diag.Warning(file, line, $"{result.Count} parameters exceeds the default VALHALLA_MAX_PARAMS (16).");
        return ok ? result : null;
    }

    void MaterializeEnum(EnumModel e)
    {
        var raw = e.Source;
        CheckKeys(raw.Args, "VALHALLA_ENUM", ["DisplayName", "Category", "Tooltip"], raw.Header, raw.Line);
        e.Meta = ReadMeta(raw.Args, raw.Name);
        if (raw.Values.Count == 0) Diag.Error(raw.Header, raw.Line, $"VALHALLA_ENUM {raw.Name} has no enumerators.");

        foreach (var v in raw.Values)
        {
            long? overrideValue = null;
            string display = Model.Literal(v.Name);
            if (v.Args != null)
            {
                CheckKeys(v.Args, "VALHALLA_ENUM_VAL", ["DisplayName", "Value"], raw.Header, v.Line);
                display = v.Args.String("DisplayName") ?? display;
                if (v.Args.Symbol("Value") is { } text)
                {
                    if (long.TryParse(text, out long parsed)) overrideValue = parsed;
                    else Diag.Error(raw.Header, v.Line, $"Value must be an integer literal, got '{text}'.");
                }
            }
            e.Values.Add(new EnumValueModel { Name = v.Name, DisplayName = display, Override = overrideValue });
        }
    }


    void ResolveParent(TypeModel type)
    {
        string file = type.Source.Header;
        int line = type.Source.Line;

        if (type.ParentSpelling != null)
        {
            var parentRef = ResolveBase(Lexer.Tokenize(type.ParentSpelling), type.Namespace, m_Inv.OwnerOf(type), file, line, reportErrors: true);
            if (parentRef?.Kind == TypeRefKind.Reflected)
            {
                type.Parent = parentRef.Type;
                string parentSimple = type.Parent!.Source.Name;
                bool inherits = type.Source.BaseNames.Any(b =>
                {
                    string head = b.Split('<')[0];
                    return head == parentSimple || head.EndsWith("::" + parentSimple);
                });
                if (!inherits)
                    Diag.Error(file, line, $"{type.Name} declares Parent={type.ParentSpelling} but does not inherit from it in C++.");
            }
            else if (parentRef != null)
            {
                Diag.Error(file, line, $"Parent '{type.ParentSpelling}' must be a VALHALLA_TYPE.");
            }
        }
    }

    void ValidateOverrides(TypeModel type)
    {
        string file = type.Source.Header;
        var chain = new HashSet<TypeModel> { type };
        for (var p = type.Parent; p != null; p = p.Parent)
        {
            if (!chain.Add(p))
            {
                Diag.Error(file, type.Source.Line, $"Parent chain of {type.Name} is cyclic.");
                type.Parent = null;
                return;
            }
        }

        foreach (var method in type.Methods.Concat(type.Operators))
        {
            if (!method.LangTraits.Contains("Overridden")) continue;
            MethodModel? baseMethod = null;
            for (var p = type.Parent; p != null && baseMethod == null; p = p.Parent)
                baseMethod = p.Methods.Concat(p.Operators).FirstOrDefault(m => m.Name == method.Name && (m.LangTraits.Contains("Override") || m.LangTraits.Contains("Overridden")));

            if (baseMethod == null)
                Diag.Error(file, method.Line, $"'{method.Name}' declares Behave.Overridden but no reflected parent declares a matching Behave.Override.");
            else if (baseMethod.LangTraits.Contains("Final"))
                Diag.Error(file, method.Line, $"'{method.Name}' overrides a parent method marked Behave.Final.");
        }
    }
}
