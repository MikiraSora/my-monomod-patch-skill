#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S69_ThrowsException : S69_ThrowsException
{
    public extern string orig_Go();

    public string Go()
    {
        var r = orig_Go();
        throw new System.InvalidOperationException("patched:" + r);
    }
}