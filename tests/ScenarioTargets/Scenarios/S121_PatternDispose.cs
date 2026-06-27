namespace MonoModTestTargets;

public class S121_TryFinallyNoUsing
{
    public bool CleanedUp;

    public string Run()
    {
        try
        {
            return "ran";
        }
        finally
        {
            CleanedUp = true;
        }
    }
}