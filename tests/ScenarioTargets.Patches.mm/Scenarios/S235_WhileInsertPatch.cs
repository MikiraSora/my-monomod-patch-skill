#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S235_WhileInsert : S235_WhileInsert
{
    // Inserted inside while body, before Step().
    public void PreStep() => Log.Append("[pre];");
}