#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S233_EnumArgInsert : S233_EnumArgInsert
{
    // Inserted between Start() and Stop(), takes an enum parameter from Current property.
    public void LogLevel(S233_Level level) => Log.Append($"[level:{level}];");
}