#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S232_CrossTypeInsert : S232_CrossTypeInsert
{
    // Inserted between Begin() and End(), calls a method on a different type.
    public void CrossNote() => S232_CrossTypeHelper.Note("[mid];");
}