namespace MonoModTestTargets;

public class S05_PatchStaticConstructor
{
    public static string StaticMarker { get; set; } = "unset";

    static S05_PatchStaticConstructor()
    {
        StaticMarker = "sctor:orig";
    }
}