namespace MonoModTestTargets;

public class S112_GotoLabel
{
    public int Loop(int n)
    {
        int sum = 0;
    start:
        if (n <= 0) return sum;
        sum += n;
        n--;
        goto start;
    }
}