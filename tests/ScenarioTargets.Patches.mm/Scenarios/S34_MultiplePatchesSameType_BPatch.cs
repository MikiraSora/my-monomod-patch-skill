#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

[MonoModPatch("global::MonoModTestTargets.S34_Multi")]
internal class S34_Multi_PatchB : S34_Multi
{
    public extern string orig_B();

    public string B() => orig_B() + "!B";
}