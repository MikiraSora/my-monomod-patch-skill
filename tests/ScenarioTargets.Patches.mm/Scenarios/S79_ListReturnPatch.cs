#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S79_ListReturn : S79_ListReturn
{
    public extern System.Collections.Generic.List<int> orig_Three();

    public System.Collections.Generic.List<int> Three()
    {
        var list = orig_Three();
        list.Add(4);
        return list;
    }
}