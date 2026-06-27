#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S223_RefParamInsert : S223_RefParamInsert
{
    // Inserted between first Bump(ref x, 10) and second Bump(ref x, 20).
    public void LogMid() => Log.Append("[mid]");
}