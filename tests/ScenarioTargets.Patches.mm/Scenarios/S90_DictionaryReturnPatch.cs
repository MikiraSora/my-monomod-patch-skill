#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S90_DictionaryReturn : S90_DictionaryReturn
{
    public extern System.Collections.Generic.Dictionary<string, int> orig_Build();

    public System.Collections.Generic.Dictionary<string, int> Build()
    {
        var d = orig_Build();
        d["b"] = 2;
        return d;
    }
}