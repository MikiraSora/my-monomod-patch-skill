#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

[MonoModPatch("global::MonoModTestTargets.S39_SealedClass")]
internal class patch_S39_SealedClass
{
    public extern string orig_Name();

    public string Name() => orig_Name() + "!";
}