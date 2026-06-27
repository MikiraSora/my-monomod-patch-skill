using System.Reflection;
using MonoMod;

var root = FindExampleRoot(AppContext.BaseDirectory);
var targetPath = Path.Combine(root, "TargetLibrary", "bin", "Debug", "net8.0", "TargetLibrary.dll");
var patchPath = Path.Combine(root, "TargetLibrary.PatchFunctionA.mm", "bin", "Debug", "TargetLibrary.PatchFunctionA.mm.dll");
var stageDir = Path.Combine(root, "artifacts", "patch-stage");
var stagedTargetPath = Path.Combine(stageDir, "TargetLibrary.dll");
var stagedPatchPath = Path.Combine(stageDir, "TargetLibrary.PatchFunctionA.mm.dll");
var outputPath = Path.Combine(stageDir, "TargetLibrary_modded.dll");

Directory.CreateDirectory(stageDir);
CopyFresh(targetPath, stagedTargetPath);
CopyFresh(patchPath, stagedPatchPath);
if (File.Exists(outputPath))
{
    File.Delete(outputPath);
}

using (var mm = new MonoModder
{
    InputPath = stagedTargetPath,
    OutputPath = outputPath
})
{
    mm.Read();
    mm.ReadMod(stagedPatchPath);
    mm.MapDependencies();
    mm.AutoPatch();
    mm.Write();
}

var assembly = Assembly.LoadFile(outputPath);
Require(assembly.GetType("MonoMod.WasHere") is not null, "patched assembly should contain MonoMod.WasHere");

var type = assembly.GetType("TargetLibrary.PatchableThing", throwOnError: true)!;
var instance = Activator.CreateInstance(type, "alpha")!;
var describe = type.GetMethod("Describe", BindingFlags.Instance | BindingFlags.Public)!;
var marker = type.GetProperty("ConstructorMarker", BindingFlags.Instance | BindingFlags.Public)!;

Require((string?)describe.Invoke(instance, new object[] { "beta" }) == "patched:ALPHA:BETA", "Describe should be patched");
Require((string?)marker.GetValue(instance) == "patched-ctor:alpha", "constructor should be patched");

Console.WriteLine("MonoMod patch DLL example passed.");
Console.WriteLine(outputPath);

static string FindExampleRoot(string start)
{
    var dir = new DirectoryInfo(start);
    while (dir is not null)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, "TargetLibrary")) &&
            Directory.Exists(Path.Combine(dir.FullName, "TargetLibrary.PatchFunctionA.mm")))
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    throw new DirectoryNotFoundException("Could not find MonoModPatchDllExample root.");
}

static void CopyFresh(string source, string destination)
{
    if (!File.Exists(source))
    {
        throw new FileNotFoundException("Expected build output is missing.", source);
    }

    File.Copy(source, destination, overwrite: true);
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
