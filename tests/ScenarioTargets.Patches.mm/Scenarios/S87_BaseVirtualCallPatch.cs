#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S87_BaseVirtual : S87_BaseVirtual
{
    public extern string orig_Name();

    public override string Name() => orig_Name() + "!";
}