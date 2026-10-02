// Root Build.cs for standalone Valhalla builds: lists the projects Valhalla-Gen reflects.
// Valhalla-Gen reads this as text; it is never compiled.
public class ValhallaRootBuild : ValhallaRoot
{
    public ValhallaRootBuild()
    {
        Modules = new[] { "tests/ValhallaSmoke" };
    }
}
