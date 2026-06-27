using MonoMod;

namespace MonoModTestTargets;

internal class patch_S63_LinkFrom : S63_LinkFrom
{
    // Redirect any call to S63_LinkFrom::Old() to this Replacement().
    // FindableID matches MonoMod's GetID: "System.String MonoModTestTargets.S63_LinkFrom::Old()"
    [MonoModLinkFrom("System.String MonoModTestTargets.S63_LinkFrom::Old()")]
    public string Replacement() => "relinked";
}