#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S60_DecimalReturn : S60_DecimalReturn
{
    public extern decimal orig_Total(decimal a, decimal b);

    public decimal Total(decimal a, decimal b) => orig_Total(a, b) + 1m;
}