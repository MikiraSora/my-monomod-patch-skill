namespace MonoModTestTargets;

public interface S45_IShape
{
    string Draw();
}

public class S45_Circle : S45_IShape
{
    public string Draw() => "circle";
}