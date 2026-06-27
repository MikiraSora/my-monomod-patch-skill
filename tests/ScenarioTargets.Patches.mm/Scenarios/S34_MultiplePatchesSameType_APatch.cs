#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

[MonoModPatch("global::MonoModTestTargets.S34_Multi")]
internal class S34_Multi_PatchA : S34_Multi
{
    public extern string orig_A();

    public string A() => orig_A() + "!A";
}