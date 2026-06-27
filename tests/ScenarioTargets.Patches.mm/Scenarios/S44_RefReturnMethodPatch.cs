#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S44_RefReturn : S44_RefReturn
{
    public extern ref int orig_Slot();

    public ref int Slot()
    {
        ref int v = ref orig_Slot();
        v += 100;
        return ref v;
    }
}