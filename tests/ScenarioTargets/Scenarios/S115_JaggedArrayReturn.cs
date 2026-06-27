namespace MonoModTestTargets;

public class S115_JaggedArrayReturn
{
    public int[][] Build() => new[] { new[] { 1 }, new[] { 2, 3 } };
}