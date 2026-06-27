#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S213_StackConsumeInsert : S213_StackConsumeInsert
{
    // No new method needed; the PostProcessor inserts only a marker call
    // between GetPrefix() return and the string concatenation that consumes it.
    // This tests inserting without disturbing the evaluation stack.
}