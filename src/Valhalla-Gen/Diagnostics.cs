namespace Valhalla.Gen;

// MSBuild-canonical format so Visual Studio surfaces Gen errors in the Error List with a clickable location.
static class Diag
{
    public static int ErrorCount { get; private set; }
    public static int WarningCount { get; private set; }

    public static void Error(string file, int line, string message)
    {
        ErrorCount++;
        Console.Error.WriteLine($"{file}({line}): error VG0001: {message}");
    }

    public static void Warning(string file, int line, string message)
    {
        WarningCount++;
        Console.WriteLine($"{file}({line}): warning VG0002: {message}");
    }

    public static void Error(string message)
    {
        ErrorCount++;
        Console.Error.WriteLine($"ValhallaGen : error VG0001: {message}");
    }

    public static void Info(string message) => Console.WriteLine($"[ValhallaGen] {message}");
}
