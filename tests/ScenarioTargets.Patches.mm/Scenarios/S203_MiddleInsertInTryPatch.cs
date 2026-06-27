#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S203_MiddleInsertInTry : S203_MiddleInsertInTry
{
    // New method added by patch. MonoModRules PostProcessor inserts a call to this
    // between A() and C() inside the try block of SafeRun().
    public void B() => Log.Append("B");
}