namespace MonoModTestTargets;

public class S63_LinkFrom
{
    public string Old() => "old";

    // Wrap calls Old(); after relinking, this call should target the patch replacement.
    public string Wrap() => Old();
}