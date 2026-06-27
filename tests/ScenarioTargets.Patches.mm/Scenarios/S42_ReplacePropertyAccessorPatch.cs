using MonoMod;

namespace MonoModTestTargets;

internal class patch_S42_ReplaceProperty : S42_ReplaceProperty
{
    // [MonoModReplace] on a get-only patch property: MonoMod removes the target
    // property metadata, backing field, and setter, but keeps the patch getter as
    // a standalone get_Label method returning the new value. The Label property
    // itself becomes absent on the patched type.
    [MonoModReplace]
    public string Label => "replaced";
}