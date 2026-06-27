#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S225_MultiParamInsert : S225_MultiParamInsert
{
    // Inserted between Start() and End(), takes Width and Height as parameters.
    public void LogDimensions(int w, int h) => Log.Append($"[{w}x{h}]");
}