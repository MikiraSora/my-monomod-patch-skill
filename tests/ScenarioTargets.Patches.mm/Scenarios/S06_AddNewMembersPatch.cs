#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S06_AddNewMembers : S06_AddNewMembers
{
    public string ExtraField;
    public string ExtraProp { get; set; }

    public string ExtraMethod() => "extra-method";

    public extern void orig_ctor();

    [MonoModConstructor]
    public void ctor()
    {
        orig_ctor();
        ExtraField = "extra";
        ExtraProp = "prop";
    }
}