#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S220_GenericMethodInsert : S220_GenericMethodInsert
{
    // Inserted after first Items.Add(1) and before Items.Add(2).
    public void LogMid() => Log.Append("[mid]");
}