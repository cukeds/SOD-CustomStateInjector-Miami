using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
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
    public List<CustomStep> ToGenerate;

    private static Dictionary<string, Action> stateGenerateMethods = new();
    
    public override void Load()
    {        
        Instance = Instance == null ? this : throw new Exception("A Plugin instance already exists.");
        CustomSteps = [];
        ToGenerate = [];
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
                Instance.ToGenerate.Add(new CustomStep { Step = customStep, After = afterStep });
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

        Log.LogInfo($"{string.Join(", ", found.Select(x => x.Step.Name))}");
        Log.LogInfo("Custom Load Order:");
        foreach (var step in found)
        {
            Log.LogInfo($"Step '{step.Step.Name}' after '{step.After.Name}'");
        }
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

    internal List<Step> FindConsecutiveSteps(List<CustomStep> customSteps, Step startingStep)
    {
        // Create a dictionary to map each step to its subsequent step
        Log.LogInfo("Finding consecutive steps...");
        Dictionary<Step, Step> stepMap = new();
        foreach (var customStep in customSteps)
        {
            Log.LogInfo($"{customStep.After.Name}, {customStep.Step.Name}");
            stepMap[customStep.After] = customStep.Step;
        }
        Log.LogInfo("Step map created!");
        // Collect all consecutive steps starting from the given starting step
        List<Step> consecutiveSteps = [];
        var currentStep = startingStep;
        while (stepMap.ContainsKey(currentStep))
        {
            Log.LogInfo(currentStep.Name);
            consecutiveSteps.Add(stepMap[currentStep]);
            currentStep = stepMap[currentStep];
            Log.LogInfo(currentStep.Name);
        }
        Log.LogInfo("Consecutive steps found!");
        return consecutiveSteps;
    }
}
    



[HarmonyPatch(typeof(CityConstructor), "Update")]
public class CityConstructor_Update_Patch
{

    /// <summary>
    ///  Passes the current load state to the Postfix method, and is responsible for executing the custom state generation.
    /// </summary>
    /// <param name="__instance"> The instance of CityContructor. </param>
    /// <param name="__state"> The state to be passed to the Postfix method. </param>
    public static void Prefix(CityConstructor __instance, out int __state)
    {
        var loadCursor = __instance.loadCursor;
        var loadState = __instance.loadState;
        var allLoadStates = Enum.GetValues(typeof(CityConstructor.LoadState)).Cast<CityConstructor.LoadState>()
            .ToList();
        var generateNew = __instance.generateNew;
        var loadingOperationActive = __instance.loadingOperationActive;
        var loadFullCityDataTask = __instance.loadFullCityDataTask;
        __state = (int)loadState;
        if (loadCursor >= allLoadStates.Count) return;
        if (!loadingOperationActive || loadFullCityDataTask is { IsCompleted: true }) return;
        if (!generateNew) return;
        var toInject = Plugin.Instance.ToGenerate.Find(x => x.Step.LoadState == loadState);
        if (toInject == null) return;
        
        var steps = Plugin.Instance.FindConsecutiveSteps(Plugin.Instance.ToGenerate, toInject.After);
        Plugin.Log.LogInfo("Generating custom states...");
        foreach (var step in steps)
        {
            Plugin.Log.LogInfo($"Generating {step.Name}...");
            Plugin.Generate(step.Name);
            Plugin.Log.LogInfo($"Generation of {step.Name} complete!");
            Plugin.Instance.ToGenerate.Remove(Plugin.Instance.ToGenerate.Find(x => x.Step.LoadState == step.LoadState));
        }
        
        __instance.loadState = toInject.After.LoadState;
    }

    
    /// <summary>
    ///  Injects the custom state into the game. Makes sure to have a return path to the original state it was before any custom state.
    /// </summary>
    /// <param name="__instance"> The instance of CityContructor. </param>
    /// <param name="__state"> The state passed from the Prefix method. </param>
    public static void Postfix(CityConstructor __instance, int __state)
    {
        var toInject =
            Plugin.Instance.ToGenerate.Find(x => x.After.LoadState == (CityConstructor.LoadState)__state);
        if (toInject == null) return;
        
        Plugin.Log.LogInfo($"Injecting {toInject.Step.Name} into {toInject.After.Name}...");
        __instance.loadState = toInject.Step.LoadState;
    }

}

