using System;
using System.Collections.Generic;
using System.Linq;
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
    
    public override void Load()
    {
        GenerateStepsFromConfig();
        // Log.LogInfo("MaxSlotAmount: " + Config.MaxSlotAmount);
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
    
    public static void GenerateStepsFromConfig()
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
                LoadState=Utils.LoadStatesLength() + customSteps.Count,
            };
            
            
            var afterStep = new Step{Name=kvp[1]};
            
            var res = customSteps.Find(x => x.Step.Name == afterStep.Name);
            if (res != null)
            {
                afterStep.LoadState = res.Step.LoadState;
            }
            else
            {
                try
                {
                    var value = (int)Enum.Parse(typeof(CityConstructor.LoadState), afterStep.Name);
                    afterStep.LoadState = value;
                }
                catch (Exception e)
                {
                    throw new Exception("Step not found in CityConstructor.LoadState nor in custom steps");
                }
            }
            customSteps.Add(new CustomStep{Step=customStep, After=afterStep});
        }
        
        foreach(var step in customSteps)
        {
            Log.LogDebug("Step: " + step.Step.Name + " LoadState: " + step.Step.LoadState + " After: " + step.After.Name + " LoadState: " + step.After.LoadState);
        }

    }
}


[HarmonyPatch(typeof(CityConstructor), "Update")]
public class CityConstructor_Update_Patch
{
    
    public static CityConstructor.LoadState generateClubs = (CityConstructor.LoadState) Enum.GetValues(typeof(CityConstructor.LoadState)).Length;
    
    [StructLayout(LayoutKind.Sequential)]
    
    public struct TaskBlittable 
    {
        public bool IsCompleted;
    }
    
    public static void Prefix(Il2CppObjectBase __instance)
    {
        var loadCursor = Plugin.GetField<int>(__instance, "loadCursor");
        var loadState = (CityConstructor.LoadState) Plugin.GetField<int>(__instance, "loadState");
        var allLoadStates = Enum.GetValues(typeof (CityConstructor.LoadState)).Cast<CityConstructor.LoadState>().ToList<CityConstructor.LoadState>();
        var generateNew = Plugin.GetField<bool>(__instance, "generateNew");
        var loadingOperationActive = Plugin.GetField<bool>(__instance, "loadingOperationActive");
        var loadFullCityDataTask = Plugin.GetField<TaskBlittable>(__instance, "loadFullCityDataTask");
        
        if (loadCursor >= allLoadStates.Count) return;
        if (loadingOperationActive && !loadFullCityDataTask.IsCompleted) return;
        if (loadState != generateClubs || !generateNew) return;
        Plugin.Log.LogInfo("Generating clubs...");
        Plugin.Log.LogInfo("Club generation complete!");
        Plugin.Log.LogInfo("Generating companies...");
        Plugin.SetField(__instance, "loadState", (int) CityConstructor.LoadState.generateCompanies);
    }
    
    public static void Postfix(Il2CppObjectBase __instance)
    {
        var loadCursor = Plugin.GetField<int>(__instance, "loadCursor");
        var loadState = (CityConstructor.LoadState) Plugin.GetField<int>(__instance, "loadState");
        var allLoadStates = Enum.GetValues(typeof (CityConstructor.LoadState)).Cast<CityConstructor.LoadState>().ToList<CityConstructor.LoadState>();
        var generateNew = Plugin.GetField<bool>(__instance, "generateNew");
        var loadingOperationActive = Plugin.GetField<bool>(__instance, "loadingOperationActive");
        var loadFullCityDataTask = Plugin.GetField<TaskBlittable>(__instance, "loadFullCityDataTask");
        
        if (loadCursor >= allLoadStates.Count) return;
        if (loadingOperationActive && !loadFullCityDataTask.IsCompleted) return;
        if (loadState != CityConstructor.LoadState.generateCompanies || !generateNew) return;
        Plugin.Log.LogInfo("Patching GenerateCompanies...");
        Plugin.SetField(__instance, "loadState", (int)generateClubs);

    }

}


// TODO TODO TODO TODO TODO TODO

// Make this a helper plugin to inject generation steps into the CityConstructor.Update method.
