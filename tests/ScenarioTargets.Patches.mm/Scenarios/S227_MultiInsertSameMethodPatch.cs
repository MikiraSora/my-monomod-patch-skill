#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S227_MultiInsertSameMethod : S227_MultiInsertSameMethod
{
    // Two insertions in Run(): after Alpha() and after Beta().
    public void AfterAlpha() => Log.Append("[a];");
    public void AfterBeta() => Log.Append("[b];");
}