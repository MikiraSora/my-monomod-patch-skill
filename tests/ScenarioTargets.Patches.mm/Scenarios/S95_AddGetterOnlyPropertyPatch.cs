#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S95_AddGetterOnlyProperty : S95_AddGetterOnlyProperty
{
    public extern void orig_ctor();

    [MonoModConstructor]
    public void ctor()
    {
        orig_ctor();
    }

    // Added getter-only property (computed from Base()).
    public int Doubled => Base() * 2;
}