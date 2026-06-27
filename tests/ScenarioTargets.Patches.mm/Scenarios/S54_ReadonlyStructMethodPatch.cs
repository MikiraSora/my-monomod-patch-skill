#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

[MonoModPatch("global::MonoModTestTargets.S54_ReadonlyStruct")]
internal class patch_S54_ReadonlyStruct
{
    public int Value;

    public extern int orig_Double();

    public int Double() => orig_Double() + 1;
}