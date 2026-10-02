using System.Text.RegularExpressions;

namespace Valhalla.Gen;

sealed class ModuleConfig
{
    public required string BuildFile { get; init; }
    public required string ModuleRoot { get; init; }
    public string ModuleName { get; set; } = "";
    public string DllIdentity { get; set; } = "";
    public string Namespace { get; set; } = "";
    public string OutputDir { get; set; } = "Generated";
    public string[] Headers { get; set; } = [];
    public string[] Dependencies { get; set; } = [];
    public string PublicTypes { get; set; } = "Visibility.Public";

    public string OutputPath => Path.GetFullPath(Path.Combine(ModuleRoot, OutputDir));
    public ulong DllHash => Fnv.Hash(DllIdentity);
}

// Build.cs is never compiled; it is read as text so module config stays in a familiar C# shape without a Roslyn dependency.
// The root Build.cs either lists project dirs (Modules = ...) for multi-module repos, or is itself the only module
// (ModuleName/Headers/...) for single-project repos. Project Build.cs files list the exact headers to scan.
static partial class BuildConfigReader
{
    [GeneratedRegex(@"\b(\w+)\s*=\s*(new\s*(?:string)?\s*\[\s*\]\s*\{[^}]*\}|""(?:[^""\\]|\\.)*""|[\w.]+)\s*;", RegexOptions.Singleline)]
    private static partial Regex AssignmentRegex();

    [GeneratedRegex(@"""((?:[^""\\]|\\.)*)""")]
    private static partial Regex StringLiteralRegex();

    static IEnumerable<(string Key, string Value)> Assignments(string file) =>
        AssignmentRegex().Matches(StripComments(File.ReadAllText(file)))
            .Select(m => (m.Groups[1].Value, m.Groups[2].Value.Trim()));

    static readonly string[] ModuleKeys = ["ModuleName", "DllIdentity", "Namespace", "OutputDir", "Headers", "Dependencies", "PublicTypes"];

    // Returns the project Build.cs paths the root Build.cs resolves to, or null if the root file is unusable.
    public static List<string>? ReadRoot(string root)
    {
        string rootFile = Path.Combine(root, "Build.cs");
        if (!File.Exists(rootFile))
        {
            Diag.Error(rootFile, 1, "Root Build.cs not found; it must either list projects (Modules = ...) or describe the single module itself.");
            return null;
        }

        var assignments = Assignments(rootFile).ToList();
        var modules = assignments.Where(a => a.Key == "Modules").Select(a => StringList(a.Value)).FirstOrDefault();
        bool isModule = assignments.Any(a => ModuleKeys.Contains(a.Key));

        if (modules != null && isModule)
        {
            Diag.Error(rootFile, 1, "Root Build.cs sets both Modules and module fields; use Modules for multi-module repos, or module fields alone for a single-project repo.");
            return null;
        }
        if (isModule) return [Path.GetFullPath(rootFile)];
        if (modules == null)
        {
            Diag.Error(rootFile, 1, "Root Build.cs must set Modules (an empty list is allowed) or describe a single module (ModuleName, Headers, ...).");
            return null;
        }

        var results = new List<string>();
        foreach (string entry in modules)
        {
            string path = Path.GetFullPath(Path.Combine(root, entry));
            string buildFile = path.EndsWith("Build.cs", StringComparison.OrdinalIgnoreCase) ? path : Path.Combine(path, "Build.cs");
            if (!File.Exists(buildFile))
            {
                Diag.Error(rootFile, 1, $"Module '{entry}' has no Build.cs ({buildFile}).");
                continue;
            }
            if (results.Contains(buildFile, StringComparer.OrdinalIgnoreCase))
            {
                Diag.Warning(rootFile, 1, $"Module '{entry}' is listed more than once.");
                continue;
            }
            results.Add(buildFile);
        }
        return results;
    }

    public static ModuleConfig? Read(string buildFile)
    {
        var config = new ModuleConfig { BuildFile = buildFile, ModuleRoot = Path.GetDirectoryName(buildFile)! };

        foreach (var (key, value) in Assignments(buildFile))
        {
            switch (key)
            {
                case "ModuleName": config.ModuleName = Unquote(value); break;
                case "DllIdentity": config.DllIdentity = Unquote(value); break;
                case "Namespace": config.Namespace = Unquote(value); break;
                case "OutputDir": config.OutputDir = Unquote(value); break;
                case "Headers": config.Headers = StringList(value); break;
                case "Dependencies": config.Dependencies = StringList(value); break;
                case "PublicTypes": config.PublicTypes = value; break;
                case "Roots":
                    Diag.Error(buildFile, 1, "Roots is no longer supported; list the reflected headers explicitly in Headers.");
                    break;
            }
        }

        bool ok = true;
        if (config.ModuleName.Length == 0) { Diag.Error(buildFile, 1, "Build.cs must set ModuleName."); ok = false; }
        if (config.DllIdentity.Length == 0) { Diag.Error(buildFile, 1, "Build.cs must set DllIdentity."); ok = false; }
        if (config.Namespace.Length == 0) { Diag.Error(buildFile, 1, "Build.cs must set Namespace."); ok = false; }
        if (config.Headers.Length == 0) Diag.Warning(buildFile, 1, "Build.cs lists no Headers; the module will reflect nothing.");
        if (config.Namespace.Length > 0 && !Regex.IsMatch(config.Namespace, @"^[A-Za-z_]\w*(::[A-Za-z_]\w*)*$"))
        {
            Diag.Error(buildFile, 1, $"Namespace '{config.Namespace}' is not a valid C++ namespace.");
            ok = false;
        }
        return ok ? config : null;
    }

    static string Unquote(string value) =>
        value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? Regex.Unescape(value[1..^1]) : value;

    static string[] StringList(string value) =>
        StringLiteralRegex().Matches(value).Select(m => Regex.Unescape(m.Groups[1].Value)).ToArray();

    static string StripComments(string text)
    {
        text = Regex.Replace(text, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        return Regex.Replace(text, @"//[^\n]*", " ");
    }
}
