#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S72_SelfAddedMethodInvoke : S72_SelfAddedMethodInvoke
{
    public extern string orig_Base();

    public string Base() => orig_Base() + ":" + Extra();

    // Added method, called from patched Base(); copied into target and callable.
    public string Extra() => "extra";
}