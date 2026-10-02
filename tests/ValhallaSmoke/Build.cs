// Valhalla-Gen reads this as text; it is never compiled.
public class ValhallaSmokeBuild : ValhallaModule
{
    public ValhallaSmokeBuild()
    {
        ModuleName   = "ValhallaSmoke";
        DllIdentity  = "ValhallaSmoke";
        Namespace    = "Smoke";
        OutputDir    = "Generated";
        Headers      = new[] { "Source/Public/SmokeTypes.h" };
        Dependencies = new[] { "ValhallaCore" };
        PublicTypes  = Visibility.Public;
    }
}
