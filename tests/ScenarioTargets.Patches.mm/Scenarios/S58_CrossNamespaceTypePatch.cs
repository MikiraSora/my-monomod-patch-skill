#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets.Patches;

// Cross-namespace patch_: MonoMod's patch_ prefix stripping uses the PATCH type's
// namespace to form the target full name, so a patch_ type in a different namespace
// would target a type in the patch's namespace (not the real target). To patch a type
// in another namespace, use [MonoModPatch("global::TargetNs.Type")] explicitly.
[MonoModPatch("global::MonoModTestTargets.SubNs.S58_CrossNamespace")]
internal class S58_CrossNamespacePatch : MonoModTestTargets.SubNs.S58_CrossNamespace
{
    public extern string orig_Tag();

    public string Tag() => orig_Tag() + "!";
}