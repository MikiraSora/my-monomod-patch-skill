#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S200_MiddleInsertVoidCall : S200_MiddleInsertVoidCall
{
    // New method added by patch. MonoModRules PostProcessor inserts a call to this
    // between First() and Third() in Run().
    public void Second() => Log.Append("2");
}