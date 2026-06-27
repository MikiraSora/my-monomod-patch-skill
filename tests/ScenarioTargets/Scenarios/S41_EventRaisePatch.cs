namespace MonoModTestTargets;

public class S41_EventRaise
{
    public event System.EventHandler? Done;

    public int Hits;

    public void Fire()
    {
        Done?.Invoke(this, System.EventArgs.Empty);
    }
}