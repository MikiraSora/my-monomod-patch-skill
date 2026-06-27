#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S228_ChainedCallInsert : S228_ChainedCallInsert
{
    // Inserted between Self() return and Done() call.
    // This is tricky: Self() returns this, and Done() consumes it on stack.
    // We insert a marker-only (stack-neutral) before Self() since the stack
    // must be empty at that point.
    // Actually we add a new method and insert it after the chain completes.
    public void PostChain() => Log.Append("[post];");
}