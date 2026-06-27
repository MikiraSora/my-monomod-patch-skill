#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S86_LockStatement : S86_LockStatement
{
    public extern int orig_Run();

    public int Run() => orig_Run() + 1;
}