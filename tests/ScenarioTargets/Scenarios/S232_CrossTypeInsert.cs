using System.Text;

namespace MonoModTestTargets;

public static class S232_CrossTypeHelper
{
    public static StringBuilder SharedLog { get; } = new();
    public static void Note(string tag) => SharedLog.Append(tag);
}

public class S232_CrossTypeInsert
{
    public void Begin() => S232_CrossTypeHelper.Note("begin;");
    public void End() => S232_CrossTypeHelper.Note("end;");

    public void Run()
    {
        Begin();
        End();
    }
}