#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S111_ByRefPropertyField : S111_ByRefPropertyField
{
    public extern ref int orig_Value();

    public ref int Value()
    {
        ref int v = ref orig_Value();
        v += 50;
        return ref v;
    }
}