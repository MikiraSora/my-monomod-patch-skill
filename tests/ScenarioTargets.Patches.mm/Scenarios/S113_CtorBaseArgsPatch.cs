#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S113_Derived : S113_Derived
{
    // C#-visible ctor chains to base; MonoMod patches the .ctor via the ctor method below.
    public patch_S113_Derived(string tag) : base(tag) { }

    public extern void orig_ctor(string tag);

    [MonoModConstructor]
    public void ctor(string tag)
    {
        orig_ctor(tag);
        Tag = Tag + "!";
    }
}