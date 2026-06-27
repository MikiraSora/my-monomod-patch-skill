#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S222_SwitchBranchInsert : S222_SwitchBranchInsert
{
    // Inserted after CaseB() call, before ldstr "two" in case 2 branch.
    public void CaseBExtra() => Log.Append("[extra]");
}