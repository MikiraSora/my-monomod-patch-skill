#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S126_AddNewEvent : S126_AddNewEvent
{
    public extern void orig_ctor();

    [MonoModConstructor]
    public void ctor()
    {
        orig_ctor();
    }

    public event System.EventHandler? Done;

    public void Fire() => Done?.Invoke(this, System.EventArgs.Empty);
}