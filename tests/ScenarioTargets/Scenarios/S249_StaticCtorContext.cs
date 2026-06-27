using System.Text;

namespace MonoModTestTargets;

public class S249_StaticCtorContext
{
    public static StringBuilder Log { get; } = new();
    public static string Tag { get; set; } = "";

    public static void Init()
    {
        Log.Append("init;");
        Tag = "ready";
    }
}