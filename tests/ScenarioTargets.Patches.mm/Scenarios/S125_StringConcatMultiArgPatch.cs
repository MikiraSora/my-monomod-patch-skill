#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S125_StringConcatMultiArg : S125_StringConcatMultiArg
{
    public extern string orig_Build(string a, int b, bool c);

    public string Build(string a, int b, bool c) => orig_Build(a, b, c) + "!";
}