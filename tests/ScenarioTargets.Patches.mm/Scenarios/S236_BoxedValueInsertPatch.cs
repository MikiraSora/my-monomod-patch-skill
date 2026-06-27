#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S236_BoxedValueInsert : S236_BoxedValueInsert
{
    // Inserted between First() and Last(), takes a boxed object parameter.
    public void LogBoxed(object value) => Log.Append($"[boxed:{value}];");
}