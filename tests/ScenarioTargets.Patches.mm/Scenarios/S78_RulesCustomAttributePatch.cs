#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S78_ValueTypeReturn : S78_ValueTypeReturn
{
    public extern S78_Result orig_Build();

    public S78_Result Build()
    {
        var r = orig_Build();
        r.Code += 10;
        return r;
    }
}