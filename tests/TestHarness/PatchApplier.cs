using MonoMod;

namespace MonoModTestHarness;

internal static class PatchApplier
{
    public static string Apply(string targetDll, string patchDll, string stageDir, string moddedName,
        Dictionary<string, object?>? sharedData = null, string[]? extraDeps = null)
    {
        Directory.CreateDirectory(stageDir);
        var stagedTarget = Path.Combine(stageDir, Path.GetFileName(targetDll));
        var stagedPatch = Path.Combine(stageDir, Path.GetFileName(patchDll));
        var moddedDll = Path.Combine(stageDir, moddedName);

        File.Copy(targetDll, stagedTarget, overwrite: true);
        File.Copy(patchDll, stagedPatch, overwrite: true);
        // Stage extra dependency DLLs so MonoMod's resolver can find them.
        if (extraDeps is not null)
        {
            foreach (var dep in extraDeps)
            {
                var dest = Path.Combine(stageDir, Path.GetFileName(dep));
                File.Copy(dep, dest, overwrite: true);
            }
        }
        if (File.Exists(moddedDll)) File.Delete(moddedDll);

        using (var mm = new MonoModder
        {
            InputPath = stagedTarget,
            OutputPath = moddedDll,
        })
        {
            // Make the staging directory a dependency search dir.
            mm.DependencyDirs.Add(stageDir);
            if (sharedData is not null)
            {
                foreach (var kv in sharedData)
                    mm.SharedData[kv.Key] = kv.Value;
            }
            mm.Read();
            mm.ReadMod(stagedPatch);
            mm.MapDependencies();
            mm.AutoPatch();
            mm.Write();
        }

        return moddedDll;
    }
}