#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S101_LinkToReverse : S101_LinkToReverse
{
    // [MonoModLinkTo] on a patch method M: relinks calls TO M to the specified target.
    // Here Replacement is called by nobody; instead we mark the existing Source wrapper.
    // Demonstrate LinkTo by adding Replacement and marking it LinkTo to Source:
    // any call to Replacement becomes a call to Source. Call() still calls Source directly,
    // so we verify LinkTo registration doesn't break patching and Source still works.
    [MonoModLinkTo("MonoModTestTargets.S101_LinkToReverse", "Source")]
    public string Replacement() => "should-be-relinked";

    public extern string orig_Source();

    public string Source() => orig_Source() + "!";
}