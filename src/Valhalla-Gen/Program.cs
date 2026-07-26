// Valhalla-Gen - offline reflection codegen tool. Pure text scanner, never an AST parser.
// Invocation: dotnet ValhallaGen.dll --root <engine_root_dir>

if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet ValhallaGen.dll --root <engine_root_dir>");
    return 1;
}

string? root = null;
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--root" && i + 1 < args.Length)
        root = args[++i];
}

if (root is null)
{
    Console.WriteLine("[ERROR] --root <engine_root_dir> is required.");
    return 1;
}

Console.WriteLine($"[ValhallaGen] Scaffolding stub - root: {root}");
return 0;
