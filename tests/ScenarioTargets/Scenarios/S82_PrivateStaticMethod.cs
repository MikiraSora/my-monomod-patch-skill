namespace MonoModTestTargets;

public class S82_PrivateStaticMethod
{
    public string Reveal() => Secret();

    private static string Secret() => "secret";
}