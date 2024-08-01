using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using SOD.Common.BepInEx;
using SOD.Common.BepInEx.Configuration;

namespace SODCustomStateInjectorMiami;


public interface IConfigBindings
{
    // Binds in the config file as: Prices.SyncDiskPrice
    [Binding("", "Key-value pair for the new step to add" +
                                                 " and what step to execute it after it, separated by commas" +
                                                 "\nValues must be in order\n" +
                                                 "Eg. generateClubs:generateCompanies, generateGuards:generateClubs\n",
        "StatesConfig.GenerationSteps")]
    string GenerationSteps { get; set; }

}

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("Venomaus.SOD.Common")]
public class Plugin : PluginController<Plugin, IConfigBindings>
{

    public static List<CustomStep> customSteps = [];
    
    private static Dictionary<string, Action> stateGenerateMethods = new Dictionary<string, Action>();
    
    public override void Load()
    {
        RegisterStateGenerateMethods();
        GenerateStepsFromConfig();
        ValidateStateGenerateMethods();
        Harmony.PatchAll();

    }
    
    public static T GetField<T>(Il2CppObjectBase instance, string fieldName)
    {
        var field = IL2CPPUtils.GetFieldIl2cpp(instance, fieldName);
        return IL2CPPUtils.GetFieldValue<T>(instance, field);
    }

    public static void SetField<T>(Il2CppObjectBase instance, string fieldName, T value)
    {
        var field = IL2CPPUtils.GetFieldIl2cpp(instance, fieldName);
        IL2CPPUtils.SetFieldIl2cpp(instance, field, value);
    }

    private static void GenerateStepsFromConfig()
    {
        var stepsStr = Instance.Config.GenerationSteps;
        if (stepsStr == "")
        {
            throw new Exception("No steps defined in config");
        }
        
        var kvpSteps = stepsStr.Split(',');
        foreach (var pair in kvpSteps)
        {
            var kvp = pair.Split(':');
            var customStep = new Step
            {
                Name=kvp[0],
                LoadState= (CityConstructor.LoadState) Utils.LoadStatesLength() + customSteps.Count,
            };
            customSteps.Add(new CustomStep{Step=customStep});

        }

        foreach (var pair in kvpSteps)
        {
            var kvp = pair.Split(":");
            var afterStep = new Step{Name=kvp[1], LoadState= GetLoadState(kvp[1])};
            var customStep = customSteps.Find(x => x.Step.Name == kvp[0]);
            customStep.After = afterStep;
        }
        
        foreach(var step in customSteps)
        {
            Log.LogInfo("Step: " + step.Step.Name + " LoadState: " + (int) step.Step.LoadState + " After: " + step.After.Name + " LoadState: " + (int) step.After.LoadState);
        }

    }
    
    public static CityConstructor.LoadState GetLoadState(string name)
    {
        var res = customSteps.Find(x => x.Step.Name == name);
        if (res != null)
        {
            return res.Step.LoadState;
        }
        
        try
        {
            return (CityConstructor.LoadState) Enum.Parse(typeof(CityConstructor.LoadState), name);
        }
        catch (Exception e)
        {
            throw new Exception($"Step {name} not found in CityConstructor.LoadState nor in custom steps");
        }
    }
    
    private static void RegisterStateGenerateMethods()
    {
        var methods = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(method => method.GetCustomAttribute<StateAttribute>() != null);

        foreach (var method in methods)
        {
            var attribute = method.GetCustomAttribute<StateAttribute>();
            if (attribute != null)
            {
                stateGenerateMethods[attribute.StateName] = (Action)Delegate.CreateDelegate(typeof(Action), method);
            }
        }
    }
    
    private static void ValidateStateGenerateMethods()
    {
        
        var notfound =customSteps.Where(step => !stateGenerateMethods.ContainsKey(step.Step.Name)).ToList();
        if (notfound.Count > 0)
        {
            throw new Exception($"StateGenerateMethods not found for: {string.Join(", ", notfound.Select(x => x.Step.Name))}");
        }
    }
    
    public static void Generate(string stateName)
    {
        if (stateGenerateMethods.TryGetValue(stateName, out var method))
        {
            method();
        }
        else
        {
            Log.LogError($"No Generate method found for state: {stateName}");
        }
    }
}


[HarmonyPatch(typeof(CityConstructor), "Update")]
public class CityConstructor_Update_Patch
{
    
    [StructLayout(LayoutKind.Sequential)]
    public struct TaskBlittable
    {
        public bool IsCompleted;
    }
    
    
    
    public static void Prefix(Il2CppObjectBase __instance, out int __state)
    {        
        var loadCursor = Plugin.GetField<int>(__instance, "loadCursor");
        var loadState = (CityConstructor.LoadState) Plugin.GetField<int>(__instance, "loadState");
        var allLoadStates = Enum.GetValues(typeof (CityConstructor.LoadState)).Cast<CityConstructor.LoadState>().ToList<CityConstructor.LoadState>();
        var generateNew = Plugin.GetField<bool>(__instance, "generateNew");
        var loadingOperationActive = Plugin.GetField<bool>(__instance, "loadingOperationActive");
        var loadFullCityDataTask = Plugin.GetField<TaskBlittable>(__instance, "loadFullCityDataTask");
        
        __state = (int) loadState;
        if (loadCursor >= allLoadStates.Count) return;
        if (loadingOperationActive && !loadFullCityDataTask.IsCompleted) return;
        if (!generateNew) return;
        var toInject = Plugin.customSteps.Find(x => x.Step.LoadState == loadState);
        if (toInject == null) return;
        Plugin.Log.LogInfo($"Generating {toInject.Step.Name}...");

        Plugin.Generate(toInject.Step.Name);
        
        Plugin.Log.LogInfo($"Generation of {toInject.Step.Name} complete!");
        Plugin.SetField(__instance, "loadState", (int) toInject.Original.LoadState);
        Plugin.SetField(__instance, "loadCursor", loadCursor - 1);
    }
    
    public static void Postfix(Il2CppObjectBase __instance, int __state)
    {
        var loadCursor = Plugin.GetField<int>(__instance, "loadCursor");
        var allLoadStates = Enum.GetValues(typeof (CityConstructor.LoadState)).Cast<CityConstructor.LoadState>().ToList();
        var generateNew = Plugin.GetField<bool>(__instance, "generateNew");
        var loadingOperationActive = Plugin.GetField<bool>(__instance, "loadingOperationActive");
        var loadFullCityDataTask = Plugin.GetField<TaskBlittable>(__instance, "loadFullCityDataTask");
        
        if (loadCursor >= allLoadStates.Count + Plugin.customSteps.Count) return;
        if (loadingOperationActive && !loadFullCityDataTask.IsCompleted) return;
        if (!generateNew) return;
        var toInject = Plugin.customSteps.Find(x => x.After.LoadState == (CityConstructor.LoadState) __state);
        if (toInject == null) return;

        try
        {
            _ = Enum.Parse(typeof(CityConstructor.LoadState), toInject.After.Name);
            toInject.Original = toInject.After;
        }
        catch (Exception e)
        {
            toInject.Original = Plugin.customSteps.Find(x => x.Step.LoadState == (CityConstructor.LoadState) __state).Original;
        }
        Plugin.Log.LogInfo($"Injecting {toInject.Step.Name} into {toInject.After.Name}...");
        Plugin.SetField(__instance, "loadState", (int) toInject.Step.LoadState);
        
    }

}
