namespace MonoModTestTargets;

public class S13_PrivateMethod
{
    public string Reveal() => Secret();

    private string Secret() => "secret";
}