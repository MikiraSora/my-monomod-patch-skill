#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S25_Derived : S25_Derived
{
    public extern string orig_Virt();

    public override string Virt() => orig_Virt() + "!";
}