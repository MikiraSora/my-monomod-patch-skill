#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S202_MiddleInsertMarker : S202_MiddleInsertMarker
{
    // New method added by patch. MonoModRules PostProcessor inserts a call to this
    // between Begin() and End() in Step(), with dnSpy-visible markers.
    public void Middle() => Log.Append("M");
}