#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S75_AddedMethodMutatesField : S75_AddedMethodMutatesField
{
    public extern void orig_ctor();

    [MonoModConstructor]
    public void ctor()
    {
        orig_ctor();
    }

    // Added method mutates an existing field on the target type.
    public void Bump(int n) => Count += n;
}