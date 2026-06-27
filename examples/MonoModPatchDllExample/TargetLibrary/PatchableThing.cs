namespace TargetLibrary;

public class PatchableThing
{
    public string Name { get; private set; }

    public string ConstructorMarker { get; protected set; }

    public PatchableThing(string name)
    {
        Name = name;
        ConstructorMarker = "original:" + name;
    }

    public virtual string Describe(string suffix)
    {
        return $"{Name}:{suffix}";
    }
}
