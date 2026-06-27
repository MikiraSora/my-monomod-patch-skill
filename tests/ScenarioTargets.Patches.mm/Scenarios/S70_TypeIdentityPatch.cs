#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S70_TypeIdentity : S70_TypeIdentity
{
    public extern string orig_TypeName();

    public string TypeName() => orig_TypeName() + "!";
}