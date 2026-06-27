namespace MonoModTestTargets;

public class S74_StaticReadonlyField
{
    public static readonly string Secret = "topsecret";

    public string Reveal() => Secret;
}