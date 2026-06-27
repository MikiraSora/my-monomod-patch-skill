#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

// Static class cannot be inherited; use [MonoModPatch] without inheritance.
[MonoModPatch("global::MonoModTestTargets.S114_Extensions")]
internal class patch_S114_Extensions
{
    public extern static string orig_Shout(string s);

    public static string Shout(string s) => orig_Shout(s) + "!";
}