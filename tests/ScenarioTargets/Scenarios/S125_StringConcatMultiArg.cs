namespace MonoModTestTargets;

public class S125_StringConcatMultiArg
{
    public string Build(string a, int b, bool c) => string.Concat(a, "-", b, "-", c);
}