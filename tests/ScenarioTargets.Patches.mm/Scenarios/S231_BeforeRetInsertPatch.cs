#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S231_BeforeRetInsert : S231_BeforeRetInsert
{
    // Inserted before the ret instruction, after First().
    public void BeforeReturn() => Log.Append("[before-ret];");
}