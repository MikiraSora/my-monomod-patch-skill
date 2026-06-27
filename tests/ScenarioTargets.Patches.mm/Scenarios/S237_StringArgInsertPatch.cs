#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S237_StringArgInsert : S237_StringArgInsert
{
    // Inserted between Alpha() and Omega(), takes a string literal argument.
    public void LogTag(string tag) => Log.Append($"[{tag}];");
}