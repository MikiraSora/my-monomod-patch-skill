#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S214_CatchBlockInsert : S214_CatchBlockInsert
{
    // Inserted inside the catch block, before Log.Append("caught").
    public void HandleCatch() => Log.Append("handled-");
}