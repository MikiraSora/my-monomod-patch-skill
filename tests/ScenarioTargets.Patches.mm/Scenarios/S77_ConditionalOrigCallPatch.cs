#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S77_ConditionalOrigCall : S77_ConditionalOrigCall
{
    public extern string orig_Echo(string s);

    public string Echo(string s)
    {
        // Only call orig_ for non-empty input; otherwise short-circuit.
        if (string.IsNullOrEmpty(s)) return "empty";
        return orig_Echo(s) + "!";
    }
}