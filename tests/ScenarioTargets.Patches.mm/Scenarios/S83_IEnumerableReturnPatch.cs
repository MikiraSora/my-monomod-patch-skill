#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S83_IEnumerableReturn : S83_IEnumerableReturn
{
    public extern System.Collections.Generic.IEnumerable<int> orig_Range();

    public System.Collections.Generic.IEnumerable<int> Range()
    {
        foreach (var v in orig_Range())
            yield return v + 100;
    }
}