#pragma warning disable CS0626

using MonoMod;

namespace TargetLibrary;

[MonoModPatch("global::TargetLibrary.PatchableThing")]
internal class PatchableThingPatch : PatchableThing
{
    public PatchableThingPatch(string name) : base(name)
    {
    }

    public extern void orig_ctor(string name);

    [MonoModConstructor]
    public void ctor(string name)
    {
        orig_ctor(name);
        ConstructorMarker = "patched-ctor:" + name;
    }

    public extern string orig_Describe(string suffix);

    public override string Describe(string suffix)
    {
        return "patched:" + orig_Describe(suffix).ToUpperInvariant();
    }

    [MonoModIgnore]
    private static string IgnoredCompileOnlyHelper(string name)
    {
        return "not copied:" + name;
    }
}
