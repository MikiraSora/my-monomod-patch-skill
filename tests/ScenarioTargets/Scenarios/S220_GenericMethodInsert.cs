using System.Collections.Generic;
using System.Text;

namespace MonoModTestTargets;

public class S220_GenericMethodInsert
{
    public List<int> Items { get; } = new();
    public StringBuilder Log { get; } = new();

    public void Populate()
    {
        Items.Add(1);
        Items.Add(2);
        Items.Add(3);
    }
}