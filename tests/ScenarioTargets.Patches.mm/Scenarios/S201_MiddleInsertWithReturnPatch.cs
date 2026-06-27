#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S201_MiddleInsertWithReturn : S201_MiddleInsertWithReturn
{
    // New method added by patch. MonoModRules PostProcessor inserts a call to this
    // after Compute() (and its stloc) and before Done() in Process().
    public void LogComputed() => Recorded = Value;
}