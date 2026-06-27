#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S109_ExplicitInterfaceMethod : S109_ExplicitInterfaceMethod
{
    // Add a public Bar that returns a different value; the explicit interface
    // impl (S109_IFoo.Bar) keeps returning "bar" (not affected by added public Bar).
    public string Bar() => "public-bar";
}