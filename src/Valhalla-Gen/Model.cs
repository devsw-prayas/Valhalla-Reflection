namespace Valhalla.Gen;

sealed class AnnotationArgs
{
    readonly Dictionary<string, List<Token>> m_Values = new(StringComparer.Ordinal);
    public string File { get; init; } = "";
    public int Line { get; init; }

    public void Set(string key, List<Token> value) => m_Values[key] = value;
    public bool Has(string key) => m_Values.ContainsKey(key);
    public IEnumerable<string> Keys => m_Values.Keys;

    public string? String(string key)
    {
        if (!m_Values.TryGetValue(key, out var v)) return null;
        if (v.Count == 1 && v[0].Kind == TokenKind.String) return v[0].Text;
        Diag.Error(File, Line, $"Specifier '{key}' expects a string literal.");
        return null;
    }

    public string? Symbol(string key) => m_Values.TryGetValue(key, out var v) ? Lexer.Join(v) : null;

    public List<string> SymbolList(string key)
    {
        if (!m_Values.TryGetValue(key, out var v)) return [];
        var inner = v.Count >= 2 && v[0].Is("{") && v[^1].Is("}") ? v[1..^1] : v;
        return Parser.SplitTopLevel(inner, ",").Select(Lexer.Join).Where(s => s.Length > 0).ToList();
    }
}

sealed class MetaStrings
{
    // Kept as C++ literal source text (quotes included) so escapes pass straight through to generated code.
    public string DisplayName = "";
    public string Category = "\"\"";
    public string Tooltip = "\"\"";
}

enum MemberKind : byte { Field, Method, Operator, Constructor }

// Members are kept as raw tokens until materialisation, so template specialisations can substitute parameters first.
sealed class RawMember
{
    public required MemberKind Kind { get; init; }
    public required AnnotationArgs Args { get; init; }
    public AnnotationArgs? Layout { get; init; }
    public required List<Token> Declaration { get; init; }
    public int Line { get; init; }
}

sealed class RawType
{
    public required string Name { get; init; }
    public required string[] Namespace { get; init; }
    public required AnnotationArgs Args { get; init; }
    public AnnotationArgs? Layout { get; init; }
    public required List<string> BaseNames { get; init; }
    public List<Token>? TemplateParams { get; init; }
    public List<string> TemplateParamNames { get; init; } = [];
    public required List<RawMember> Members { get; init; }
    public required string Header { get; init; }
    public int Line { get; init; }
    public required ModuleConfig Module { get; init; }

    public bool IsTemplate => TemplateParams != null;
    public string QualifiedName => Model.Qualify(Namespace, Name);
}

sealed class RawEnumValue
{
    public required string Name { get; init; }
    public AnnotationArgs? Args { get; init; }
    public int Line { get; init; }
}

sealed class RawEnum
{
    public required string Name { get; init; }
    public required string[] Namespace { get; init; }
    public required AnnotationArgs Args { get; init; }
    public string? Underlying { get; init; }
    public required List<RawEnumValue> Values { get; init; }
    public required string Header { get; init; }
    public int Line { get; init; }
    public required ModuleConfig Module { get; init; }
    public string QualifiedName => Model.Qualify(Namespace, Name);
}

sealed class RawSpecialization
{
    public required List<Token> Tokens { get; init; }
    public required string[] Namespace { get; init; }
    public required string Header { get; init; }
    public int Line { get; init; }
    public required ModuleConfig Module { get; init; }
}

enum TypeRefKind : byte { Void, Primitive, Reflected, Enum }

sealed class TypeRef
{
    public TypeRefKind Kind;
    public string Primitive = "";
    public TypeModel? Type;
    public EnumModel? Enum;
    public bool IsPointer;
    public bool IsReference;
    public bool IsConst;

    public ulong Hash => Kind switch
    {
        TypeRefKind.Primitive => Fnv.Hash(Primitive),
        TypeRefKind.Reflected => Type!.Hash,
        TypeRefKind.Enum => Enum!.Hash,
        _ => 0,
    };
}

sealed class ParamModel
{
    public required string Name;
    public required string TypeText;
    public required TypeRef Type;
}

sealed class FieldModel
{
    public required string Name;
    public required string TypeText;
    public required TypeRef Type;
    public required MetaStrings Meta;
    public required string Visibility;
    public List<string> LangTraits = [];
    public List<string> ReflectTraits = [];
    public List<string> ToolTraits = [];
    public int? Align;
    public bool IsStatic;
    public int Line;
}

sealed class MethodModel
{
    public required string Name;          // C++ name as written, e.g. "SetColor" or "operator=="
    public required string ReturnText;
    public required TypeRef ReturnType;
    public required List<ParamModel> Params;
    public required MetaStrings Meta;
    public required string Visibility;
    public List<string> LangTraits = [];
    public List<string> ReflectTraits = [];
    public List<string> ToolTraits = [];
    public bool IsStatic;
    public bool IsConst;
    public bool IsAbstract;
    public string? Operator;              // OperatorKind enumerator for VALHALLA_OPERATOR members
    public string Ident = "";             // unique C++ identifier for CTLS/thunk names
    public int Line;
}

sealed class ConstructorModel
{
    public required string Kind;          // Default / Copy / Move / Custom
    public required string Visibility;
    public List<ParamModel> Params = [];
    public string Ident = "";
    public int Line;
}

sealed class TypeModel
{
    public required string Name;          // "LumosLight" or "RArray<float>"
    public required string[] Namespace;
    public required RawType Source;
    public string? SpecializationArgsEmit; // "::Lumos::Mat, float" for specialisations
    public bool IsClass;
    public MetaStrings Meta = new();
    public int? Align;
    public string? ParentSpelling;
    public TypeModel? Parent;
    public List<FieldModel> Fields = [];
    public List<MethodModel> Methods = [];
    public List<MethodModel> Operators = [];
    public List<ConstructorModel> Constructors = [];

    public ModuleConfig Module => Source.Module;
    public string QualifiedName => Model.Qualify(Namespace, Name);
    public ulong Hash => Fnv.Hash(QualifiedName);
    public string EmitName => SpecializationArgsEmit == null ? Name : $"{Source.Name}<{SpecializationArgsEmit}>";
    public string FullEmitName => "::" + Model.Qualify(Namespace, EmitName);
    public string Ident => Model.Identifier(QualifiedName);
    public string DefaultVisibility => IsClass ? "Private" : "Public";
}

sealed class EnumValueModel
{
    public required string Name;
    public required string DisplayName;   // C++ literal
    public long? Override;
}

sealed class EnumModel
{
    public required RawEnum Source;
    public MetaStrings Meta = new();
    public List<EnumValueModel> Values = [];
    public string Name => Source.Name;
    public string[] Namespace => Source.Namespace;
    public ModuleConfig Module => Source.Module;
    public string QualifiedName => Source.QualifiedName;
    public ulong Hash => Fnv.Hash(QualifiedName);
    public string FullEmitName => "::" + QualifiedName;
    public string Ident => Model.Identifier(QualifiedName);
}

static class Model
{
    public static string Qualify(string[] ns, string name) => ns.Length == 0 ? name : string.Join("::", ns) + "::" + name;

    public static string Identifier(string text)
    {
        var chars = text.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        string s = new string(chars);
        while (s.Contains("__")) s = s.Replace("__", "_");
        return s.Trim('_');
    }

    public static string Literal(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
