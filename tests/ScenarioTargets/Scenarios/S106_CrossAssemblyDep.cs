namespace MonoModTestTargets;

public class S106_CrossAssemblyDep
{
    public int Compute(int x) => MonoModHelperLib.HelperMath.Double(x);
}