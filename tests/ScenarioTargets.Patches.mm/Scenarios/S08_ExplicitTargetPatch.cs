#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets.Patches;

[MonoModPatch("global::MonoModTestTargets.S08_ExplicitTarget")]
internal class S08_ExplicitPatch : MonoModTestTargets.S08_ExplicitTarget
{
    public extern string orig_Label();

    public string Label() => orig_Label() + "!";
}