#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S76_StringInterpolation : S76_StringInterpolation
{
    public extern string orig_Build(string a, int b);

    public string Build(string a, int b) => "[" + orig_Build(a, b) + "]";
}