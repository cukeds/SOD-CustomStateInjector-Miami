using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using SODCustomStateInjectorMiami.Attributes;



namespace SODCustomStateInjectorMiami;


/// <summary>
///   This class is used to inject custom states into the game.
/// </summary>
public static class CustomStateInjector
{
    /// <summary>
    ///     Injects the custom states into the game.
    /// </summary>
    public static void InjectStates()
    {
        Plugin.Instance.Start();
    }
}

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    
    public new static ManualLogSource Log;
    
    Harmony Harmony;
    
    public static Plugin Instance { get; private set; }
    
    public List<CustomStep> CustomSteps;

    private static Dictionary<string, Action> stateGenerateMethods = new();
    
    public override void Load()
    {        
        Instance = Instance == null ? this : throw new Exception("A Plugin instance already exists.");
        CustomSteps = [];
        Harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        Log = base.Log;
        Harmony.PatchAll();
    }

    
    public void Start()
    {
        RegisterCustomStates();
        RegisterStateGenerateMethods();
        ValidateStateGenerateMethods();
    }

    
    /// <summary>
    ///   Gets the value of a field from Il2CPP.
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="fieldName"></param>
    /// <typeparam name="T"> The type to cast the field. Has to be blittable.</typeparam>
    /// <returns> The value of the field.</returns>
    public static T GetField<T>(Il2CppObjectBase instance, string fieldName)
    {
        var field = IL2CPPUtils.GetFieldIl2cpp(instance, fieldName);
        return IL2CPPUtils.GetFieldValue<T>(instance, field);
    }

    /// <summary>
    ///  Sets the value of a field from Il2CPP.
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="fieldName"></param>
    /// <param name="value"> The value to set the field to.</param>
    /// <typeparam name="T"> The type of the value to set. Has to be blittable.</typeparam>
    public static void SetField<T>(Il2CppObjectBase instance, string fieldName, T value)
    {
        var field = IL2CPPUtils.GetFieldIl2cpp(instance, fieldName);
        IL2CPPUtils.SetFieldIl2cpp(instance, field, value);
    }

    
    /// <summary>
    ///  Gets the Enum value of a step based on the name.
    /// </summary>
    /// <param name="name">The name of the step.</param>
    /// <returns> The Enum value of the custom step.</returns>
    /// <exception cref="Exception"> If the step is not found in the custom steps or in the CityConstructor.LoadState.</exception>
    public CityConstructor.LoadState GetLoadState(string name)
    {
        var res = CustomSteps.Find(x => x.Step.Name == name);
        if (res != null)
        {
            return res.Step.LoadState;
        }

        try
        {
            return (CityConstructor.LoadState)Enum.Parse(typeof(CityConstructor.LoadState), name);
        }
        catch (Exception)
        {
            throw new Exception($"Step {name} not found in CityConstructor.LoadState nor in custom steps");
        }
    }

    /// <summary>
    ///  Registers the custom states into the game based on the CustomStateAttribute.
    /// </summary>
    private static void RegisterCustomStates()
    {
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.GetCustomAttributes<CustomStateAttribute>(true).Any());

        foreach (var type in types)
        {
            var attributes = type.GetCustomAttributes<CustomStateAttribute>(true);
            foreach (var attribute in attributes)
            {
                var customStep = new Step
                {
                    Name = attribute.StepName,
                    LoadState = (CityConstructor.LoadState)Utils.LoadStatesLength() + Instance.CustomSteps.Count,
                };

                var afterStep = new Step { Name = attribute.AfterStepName, LoadState = Instance.GetLoadState(attribute.AfterStepName) };
                Instance.CustomSteps.Add(new CustomStep { Step = customStep, After = afterStep });
            }
        }
    }

    /// <summary>
    ///  Registers the state generation methods based on the GenerateStateAttribute.
    /// </summary>
    private static void RegisterStateGenerateMethods()
    {
        var methods = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                                BindingFlags.NonPublic))
            .Where(method => method.GetCustomAttribute<GenerateStateAttribute>() != null);

        foreach (var method in methods)
        {
            var attribute = method.GetCustomAttribute<GenerateStateAttribute>();
            if (attribute != null)
            {
                stateGenerateMethods[attribute.StateName] = (Action)Delegate.CreateDelegate(typeof(Action), method);
            }
        }
    }

    /// <summary>
    ///  Validates that every custom step has a generate method, and gives a warning if not.
    /// </summary>
    private void ValidateStateGenerateMethods()
    {
        var found = CustomSteps.Where(step => stateGenerateMethods.ContainsKey(step.Step.Name)).ToList();
        var notfound = CustomSteps.Where(step => !stateGenerateMethods.ContainsKey(step.Step.Name)).ToList();
        if (notfound.Count > 0)
        {
            Log.LogWarning(
                $"Generate Methods not found for: {string.Join(", ", notfound.Select(x => x.Step.Name))}");
        }

        Log.LogInfo($"{string.Join(", ", found.Select(x => x.Step.Name))} added to custom steps");
    }

    
    /// <summary>
    ///  Generates a state based on the state name, used to call the generate methods.
    /// </summary>
    /// <param name="stateName"> The name of the state. </param>
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


    /// <summary>
    ///  Passes the current load state to the Postfix method, and is responsible for executing the custom state generation.
    /// </summary>
    /// <param name="__instance"> The instance of CityContructor in Il2cpp. </param>
    /// <param name="__state"> The state to be passed to the Postfix method. </param>
    public static void Prefix(Il2CppObjectBase __instance, out int __state)
    {
        var loadCursor = Plugin.GetField<int>(__instance, "loadCursor");
        var loadState = (CityConstructor.LoadState)Plugin.GetField<int>(__instance, "loadState");
        var allLoadStates = Enum.GetValues(typeof(CityConstructor.LoadState)).Cast<CityConstructor.LoadState>()
            .ToList();
        var generateNew = Plugin.GetField<bool>(__instance, "generateNew");
        var loadingOperationActive = Plugin.GetField<bool>(__instance, "loadingOperationActive");
        var loadFullCityDataTask = Plugin.GetField<TaskBlittable>(__instance, "loadFullCityDataTask");

        __state = (int)loadState;
        if (loadCursor >= allLoadStates.Count) return;
        if (loadingOperationActive && !loadFullCityDataTask.IsCompleted) return;
        if (!generateNew) return;
        var toInject = Plugin.Instance.CustomSteps.Find(x => x.Step.LoadState == loadState);
        if (toInject == null) return;
        Plugin.Log.LogInfo($"Generating {toInject.Step.Name}...");

        Plugin.Generate(toInject.Step.Name);

        Plugin.Log.LogInfo($"Generation of {toInject.Step.Name} complete!");
        Plugin.SetField(__instance, "loadState", (int)toInject.Original.LoadState);
        Plugin.SetField(__instance, "loadCursor", loadCursor - 1);
    }

    
    /// <summary>
    ///  Injects the custom state into the game. Makes sure to have a return path to the original state it was before any custom state.
    /// </summary>
    /// <param name="__instance"> The instance of CityContructor in Il2cpp. </param>
    /// <param name="__state"> The state passed from the Prefix method. </param>
    public static void Postfix(Il2CppObjectBase __instance, int __state)
    {
        var loadCursor = Plugin.GetField<int>(__instance, "loadCursor");
        var allLoadStates = Enum.GetValues(typeof(CityConstructor.LoadState)).Cast<CityConstructor.LoadState>()
            .ToList();
        var generateNew = Plugin.GetField<bool>(__instance, "generateNew");
        var loadingOperationActive = Plugin.GetField<bool>(__instance, "loadingOperationActive");
        var loadFullCityDataTask = Plugin.GetField<TaskBlittable>(__instance, "loadFullCityDataTask");

        if (loadCursor >= allLoadStates.Count + Plugin.Instance.CustomSteps.Count) return;
        if (loadingOperationActive && !loadFullCityDataTask.IsCompleted) return;
        if (!generateNew) return;
        var toInject =
            Plugin.Instance.CustomSteps.Find(x => x.After.LoadState == (CityConstructor.LoadState)__state);
        if (toInject == null) return;

        try
        {
            _ = Enum.Parse(typeof(CityConstructor.LoadState), toInject.After.Name);
            toInject.Original = toInject.After;
        }
        catch (Exception)
        {
            toInject.Original = Plugin.Instance.CustomSteps
                .Find(x => x.Step.LoadState == (CityConstructor.LoadState)__state).Original;
        }

        Plugin.Log.LogInfo($"Injecting {toInject.Step.Name} into {toInject.After.Name}...");
        Plugin.SetField(__instance, "loadState", (int)toInject.Step.LoadState);

    }

}

