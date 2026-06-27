#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S106_CrossAssemblyDep : S106_CrossAssemblyDep
{
    public extern int orig_Compute(int x);

    public int Compute(int x) => orig_Compute(x) + 1;
}