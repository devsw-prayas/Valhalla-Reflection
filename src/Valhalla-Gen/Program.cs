// Valhalla-Gen - offline reflection codegen tool. Pure text scanner, never an AST parser.
// Invocation: dotnet ValhallaGen.dll --root <engine_root_dir> [--primitives <ValhallaPrimitives.def>] [--registered <file>] [--verbose]
// <engine_root_dir>/Build.cs lists the reflected projects; each project's Build.cs lists the headers to scan.
using Valhalla.Gen;

string? root = null;
string? primitivesPath = null;
string? registeredPath = null;
bool verbose = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--root" when i + 1 < args.Length: root = args[++i]; break;
        case "--primitives" when i + 1 < args.Length: primitivesPath = args[++i]; break;
        case "--registered" when i + 1 < args.Length: registeredPath = args[++i]; break;
        case "--verbose": verbose = true; break;
        default:
            Console.WriteLine($"[ERROR] Unknown argument '{args[i]}'.");
            Console.WriteLine("Usage: dotnet ValhallaGen.dll --root <engine_root_dir> [--primitives <def>] [--registered <file>] [--verbose]");
            return 1;
    }
}

if (root is null || !Directory.Exists(root))
{
    Console.WriteLine("[ERROR] --root <engine_root_dir> is required and must exist.");
    return 1;
}
root = Path.GetFullPath(root);

primitivesPath ??= FindPrimitivesDef();
if (primitivesPath is null || !File.Exists(primitivesPath))
{
    Diag.Error("Could not locate ValhallaPrimitives.def; pass --primitives <path>.");
    return 1;
}
var primitives = PrimitiveTable.Load(primitivesPath);

var moduleFiles = BuildConfigReader.ReadRoot(root);
if (moduleFiles is null) return 1;
var modules = moduleFiles
    .Select(BuildConfigReader.Read)
    .OfType<ModuleConfig>()
    .ToList();

foreach (var dup in modules.GroupBy(m => m.ModuleName).Where(g => g.Count() > 1))
    Diag.Error(dup.Last().BuildFile, 1, $"ModuleName '{dup.Key}' is already used by {dup.First().BuildFile}.");
foreach (var dup in modules.GroupBy(m => m.DllHash).Where(g => g.Count() > 1))
    Diag.Error(dup.Last().BuildFile, 1, $"DllIdentity '{dup.Last().DllIdentity}' collides with {dup.First().BuildFile}.");
foreach (var m in modules)
    foreach (string dep in m.Dependencies.Where(d => d != "ValhallaCore" && modules.All(o => o.ModuleName != d)))
        if (verbose) Diag.Info($"{m.ModuleName}: dependency '{dep}' has no Build.cs; its types cannot be referenced.");

// CMake writes one module dir per valhalla_reflect() call; both sides must agree or generated code goes uncompiled.
if (registeredPath != null)
{
    if (!File.Exists(registeredPath))
    {
        Diag.Error($"Registered-modules file '{registeredPath}' does not exist; re-run CMake configure.");
        return 1;
    }
    static string Norm(string p) => Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    var registered = File.ReadAllLines(registeredPath).Where(l => l.Trim().Length > 0).Select(Norm).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var listed = modules.Select(m => Norm(m.ModuleRoot)).ToHashSet(StringComparer.OrdinalIgnoreCase);
    string rootFile = Path.Combine(root, "Build.cs");

    foreach (var m in modules.Where(m => !registered.Contains(Norm(m.ModuleRoot))))
        Diag.Error(m.BuildFile, 1, $"Module '{m.ModuleName}' is listed in the root Build.cs but no CMake target calls valhalla_reflect() for {m.ModuleRoot}; its generated code would never be compiled.");
    foreach (string dir in registered.Where(d => !listed.Contains(d)))
        Diag.Error(rootFile, 1, $"A CMake target calls valhalla_reflect() for {dir}, but the root Build.cs does not list it.");
}

var scan = new ScanResult();
foreach (var module in modules)
{
    foreach (string entry in module.Headers)
    {
        string header = Path.GetFullPath(Path.Combine(module.ModuleRoot, entry));
        if (!File.Exists(header))
        {
            Diag.Error(module.BuildFile, 1, $"Header '{entry}' does not exist ({header}).");
            continue;
        }
        if (!Parser.ParseFile(header, module, scan))
            Diag.Warning(module.BuildFile, 1, $"Header '{entry}' is listed but contains no Valhalla annotations.");
    }
}

var inventory = Resolver.Resolve(scan, primitives);

// Nothing is written once any error is known: a half-updated Generated/ dir is worse than a stale one.
var emitters = new List<ModuleEmitter>();
if (Diag.ErrorCount == 0)
{
    foreach (var module in modules)
    {
        var emitter = new ModuleEmitter(module, inventory);
        emitter.Emit();
        emitters.Add(emitter);
    }
}

if (Diag.ErrorCount > 0)
{
    Console.Error.WriteLine($"[ValhallaGen] Failed with {Diag.ErrorCount} error(s); generated files left untouched.");
    return 1;
}

int written = 0, unchanged = 0, removed = 0;
for (int i = 0; i < modules.Count; i++)
{
    var module = modules[i];
    Directory.CreateDirectory(module.OutputPath);
    foreach (var (name, content) in emitters[i].Files)
    {
        string path = Path.Combine(module.OutputPath, name);
        string normalized = content.Replace("\n", Environment.NewLine);
        // Content comparison instead of timestamps: untouched files keep their mtime, so nothing downstream rebuilds.
        if (File.Exists(path) && File.ReadAllText(path) == normalized) { unchanged++; continue; }
        File.WriteAllText(path, normalized);
        written++;
        if (verbose) Diag.Info($"wrote {path}");
    }

    foreach (string stale in Directory.EnumerateFiles(module.OutputPath, "*.generated.*"))
    {
        if (emitters[i].Files.ContainsKey(Path.GetFileName(stale))) continue;
        File.Delete(stale);
        removed++;
        if (verbose) Diag.Info($"removed stale {stale}");
    }
}

Diag.Info($"{modules.Count} module(s), {inventory.Types.Count} type(s), {inventory.Enums.Count} enum(s): " +
          $"{written} written, {unchanged} unchanged, {removed} removed" +
          (Diag.WarningCount > 0 ? $", {Diag.WarningCount} warning(s)." : "."));
return 0;

static string? FindPrimitivesDef()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
    {
        string candidate = Path.Combine(dir.FullName, "ValhallaCore", "src", "Public", "ValhallaPrimitives.def");
        if (File.Exists(candidate)) return candidate;
        candidate = Path.Combine(dir.FullName, "src", "ValhallaCore", "src", "Public", "ValhallaPrimitives.def");
        if (File.Exists(candidate)) return candidate;
    }
    return null;
}
