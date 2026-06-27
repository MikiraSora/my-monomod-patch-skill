namespace MonoModTestTargets;

public enum S57_Color { Red, Green, Blue }

public class S57_EnumArg
{
    public string Name(S57_Color c) => "color:" + c;
}