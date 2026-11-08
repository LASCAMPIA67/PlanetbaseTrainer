// Verifies, by metadata only (the game is not started), that every Harmony patch of PlanetbaseTrainer.dll
// resolves to exactly one method of the installed Assembly-CSharp.dll, that injected parameters exist with
// compatible types, and that every member accessed by name through reflection still exists.
//
// Usage: PatchVerifier [gameDir] [pluginDll]
using System.Reflection;

const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

string gameDir = args.Length > 0 ? args[0] : FindGameDir();
string managedDir = Path.Combine(gameDir, "Planetbase_Data", "Managed");
string bepInExCore = Path.Combine(gameDir, "BepInEx", "core");
string pluginPath = args.Length > 1
    ? args[1]
    : Path.Combine(gameDir, "BepInEx", "plugins", "PlanetbaseTrainer", "PlanetbaseTrainer.dll");

foreach (var required in new[] { managedDir, bepInExCore, pluginPath })
{
    if (!Path.Exists(required))
    {
        Console.Error.WriteLine($"Not found: {required}");
        return 2;
    }
}

var assemblyPaths = Directory.GetFiles(managedDir, "*.dll")
    .Concat(Directory.GetFiles(bepInExCore, "*.dll"))
    .Append(pluginPath)
    .GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
    .Select(g => g.First())
    .ToList();

using var context = new MetadataLoadContext(new PathAssemblyResolver(assemblyPaths), "mscorlib");
var game = context.LoadFromAssemblyPath(Path.Combine(managedDir, "Assembly-CSharp.dll"));
var plugin = context.LoadFromAssemblyPath(pluginPath);

int passed = 0, failed = 0;
void Pass(string message) { passed++; Console.WriteLine($"  OK    {message}"); }
void Fail(string message) { failed++; Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine($"  FAIL  {message}"); Console.ResetColor(); }

Console.WriteLine($"Game   : {gameDir}");
Console.WriteLine($"Plugin : {pluginPath}");
Console.WriteLine();
Console.WriteLine("== Harmony patches");

foreach (var patchClass in plugin.GetTypes().OrderBy(t => t.FullName))
{
    var attributes = patchClass.GetCustomAttributesData()
        .Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch")
        .ToList();
    if (attributes.Count == 0)
        continue;

    Type? targetType = null;
    string? methodName = null;
    Type[]? argumentTypes = null;
    bool constructor = false;
    foreach (var argument in attributes.SelectMany(a => a.ConstructorArguments))
    {
        switch (argument.ArgumentType.FullName)
        {
            case "System.Type":
                targetType = (Type?)argument.Value;
                break;
            case "System.String":
                methodName = (string?)argument.Value;
                break;
            case "System.Type[]":
                argumentTypes = ((IEnumerable<CustomAttributeTypedArgument>)argument.Value!).Select(x => (Type)x.Value!).ToArray();
                break;
            case "HarmonyLib.MethodType":
                constructor = Convert.ToInt32(argument.Value) == 3; // MethodType.Constructor
                break;
        }
    }

    string label = $"{patchClass.Name,-48}";
    if (targetType == null || (!constructor && methodName == null))
    {
        Fail($"{label} incomplete HarmonyPatch attribute");
        continue;
    }

    MethodBase? target = constructor ? FindConstructor(targetType, argumentTypes) : FindMethod(targetType, methodName!, argumentTypes, out string? error);
    if (target == null)
    {
        Fail($"{label} target not found: {targetType.Name}.{(constructor ? ".ctor" : methodName)}");
        continue;
    }

    var problems = CheckPatchMethods(patchClass, target).ToList();
    if (problems.Count == 0)
        Pass($"{label} -> {target.DeclaringType!.Name}.{target.Name}({string.Join(", ", target.GetParameters().Select(p => p.ParameterType.Name))})");
    else
        foreach (var problem in problems)
            Fail($"{label} {problem}");
}

Console.WriteLine();
Console.WriteLine("== Members accessed through reflection");
var reflected = new (string Type, string Member, string? FieldType)[]
{
    ("Planetbase.GameManager", "mInstance", "Planetbase.GameManager"),
    ("Planetbase.GameManager", "mState", "Planetbase.GameManager+State"),
    ("Planetbase.Character", "mCharacters", "System.Collections.Generic.List`1"),
    ("Planetbase.Construction", "mConstructions", "System.Collections.Generic.List`1"),
    ("Planetbase.Character", "mIndicators", "Planetbase.Indicator[]"),
    ("Planetbase.Construction", "mConditionIndicator", "Planetbase.Indicator"),
    ("Planetbase.ConstructionComponent", "mConditionIndicator", "Planetbase.Indicator"),
    ("Planetbase.Resource", "mConditionIndicator", "Planetbase.Indicator"),
    ("Planetbase.Sandstorm", "mTime", "System.Single"),
    ("Planetbase.Sandstorm", "mSandstormTime", "System.Single"),
    ("Planetbase.Blizzard", "mTime", "System.Single"),
    ("Planetbase.Blizzard", "mBlizzardTime", "System.Single"),
    ("Planetbase.SolarFlare", "mTime", "System.Single"),
    ("Planetbase.SolarFlare", "mSolarFlareTime", "System.Single"),
    ("Planetbase.GuiWindow", "mRootItem", "Planetbase.GuiLabelItem"),
    ("Planetbase.GuiLabelItem", "mCallback", "Planetbase.GuiDefinitions+Callback"),
    ("Planetbase.GuiMerchantWindow", "mMerchantStockAmounts", "Planetbase.ProductAmounts"),
    ("Planetbase.GuiMerchantWindow", "mMerchantTradeAmounts", "Planetbase.ProductAmounts"),
    ("Planetbase.CameraManager", "mZoomAxis", "System.Single"),
    ("Planetbase.TimeManager", "onTimeScaleChanged()", null),
    ("Planetbase.GuiMerchantWindow", "updateUi()", null),
    ("Planetbase.Util", "getFilesFolder()", null), // Self-test only: keeps the test's profile in its output folder.
};
foreach (var (typeName, member, fieldType) in reflected)
{
    var type = game.GetType(typeName);
    if (type == null)
    {
        Fail($"type {typeName} not found");
        continue;
    }
    if (member.EndsWith("()"))
    {
        string name = member[..^2];
        var method = type.GetMethods(All).FirstOrDefault(m => m.Name == name && m.GetParameters().Length == 0);
        if (method != null) Pass($"{type.Name}.{member}");
        else Fail($"{type.Name}.{member} not found");
        continue;
    }
    var field = type.GetField(member, All);
    if (field == null)
        Fail($"{type.Name}.{member} not found");
    else if (fieldType != null && !(field.FieldType.FullName ?? field.FieldType.Name).StartsWith(fieldType, StringComparison.Ordinal))
        Fail($"{type.Name}.{member}: type {field.FieldType.FullName} (expected {fieldType})");
    else
        Pass($"{type.Name}.{member}: {field.FieldType.Name}");
}

Console.WriteLine();
Console.ForegroundColor = failed == 0 ? ConsoleColor.Green : ConsoleColor.Red;
Console.WriteLine($"Result: {passed} passed, {failed} failed");
Console.ResetColor();
return failed == 0 ? 0 : 1;

// ---------------------------------------------------------------------------------------------- helpers

static MethodBase? FindConstructor(Type type, Type[]? argumentTypes) =>
    type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        .FirstOrDefault(c => argumentTypes == null || SameTypes(c.GetParameters(), argumentTypes));

// Mirrors AccessTools.Method: search the type, then its base types; without argument types the name must be unique.
static MethodInfo? FindMethod(Type type, string name, Type[]? argumentTypes, out string? error)
{
    error = null;
    for (var current = type; current != null; current = current.BaseType)
    {
        var candidates = current
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == name)
            .ToList();
        if (candidates.Count == 0)
            continue;
        if (argumentTypes != null)
            return candidates.FirstOrDefault(m => SameTypes(m.GetParameters(), argumentTypes));
        if (candidates.Count > 1)
        {
            error = "multiple overloads";
            return null;
        }
        return candidates[0];
    }
    return null;
}

static bool SameTypes(ParameterInfo[] parameters, Type[] types) =>
    parameters.Length == types.Length &&
    parameters.Zip(types).All(pair => pair.First.ParameterType.FullName == pair.Second.FullName);

static IEnumerable<string> CheckPatchMethods(Type patchClass, MethodBase target)
{
    var patchMethods = patchClass
        .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Where(m => m.Name is "Prefix" or "Postfix" or "Finalizer" or "Transpiler")
        .ToList();
    if (patchMethods.Count == 0)
        yield return "no Prefix/Postfix method";

    var targetParameters = target.GetParameters();
    var returnType = target is MethodInfo info ? info.ReturnType : null;
    foreach (var method in patchMethods)
    {
        if (method.Name == "Prefix" && method.ReturnType.FullName is not ("System.Void" or "System.Boolean"))
            yield return "Prefix doit retourner void ou bool";
        foreach (var parameter in method.GetParameters())
        {
            var parameterType = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType()! : parameter.ParameterType;
            switch (parameter.Name)
            {
                case "__instance":
                    if (target.IsStatic)
                        yield return "__instance on a static method";
                    else if (!IsAssignableTo(target.DeclaringType!, parameterType))
                        yield return $"__instance de type {parameterType.Name} incompatible avec {target.DeclaringType!.Name}";
                    break;
                case "__result":
                    if (returnType == null || returnType.FullName == "System.Void")
                        yield return "__result on a void method";
                    else if (returnType.FullName != parameterType.FullName)
                        yield return $"__result is {parameterType.Name} instead of {returnType.Name}";
                    break;
                case "__state":
                case "__originalMethod":
                case "__args":
                case "__runOriginal":
                    break;
                default:
                    var match = targetParameters.FirstOrDefault(p => p.Name == parameter.Name);
                    if (match == null)
                        yield return $"parameter '{parameter.Name}' does not exist on the target";
                    else
                    {
                        var matchType = match.ParameterType.IsByRef ? match.ParameterType.GetElementType()! : match.ParameterType;
                        if (matchType.FullName != parameterType.FullName)
                            yield return $"parameter '{parameter.Name}' is {parameterType.Name} instead of {matchType.Name}";
                    }
                    break;
            }
        }
    }
}

static bool IsAssignableTo(Type derived, Type baseType)
{
    for (var current = derived; current != null; current = current.BaseType)
        if (current.FullName == baseType.FullName)
            return true;
    return false;
}

// Same resolution order as Directory.Build.props: PLANETBASE_DIR, a parent folder (repository cloned inside the game
// folder), Steam's install record for Planetbase (app 403190), then the default Steam library.
static string FindGameDir()
{
    var fromEnvironment = Environment.GetEnvironmentVariable("PLANETBASE_DIR");
    if (!string.IsNullOrEmpty(fromEnvironment))
        return fromEnvironment;
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "Planetbase.exe")))
            return dir.FullName;
    return FindSteamInstallLocation() ?? @"C:\Program Files (x86)\Steam\steamapps\common\Planetbase";
}

static string? FindSteamInstallLocation()
{
    if (!OperatingSystem.IsWindows())
        return null;
    foreach (var view in new[] { Microsoft.Win32.RegistryView.Registry64, Microsoft.Win32.RegistryView.Registry32 })
    {
        using var root = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, view);
        using var key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 403190");
        if (key?.GetValue("InstallLocation") is string location && Directory.Exists(location))
            return location;
    }
    return null;
}
