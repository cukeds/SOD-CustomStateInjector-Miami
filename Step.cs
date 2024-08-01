using System;

namespace SODCustomStateInjectorMiami;

public class Step()
{
    public string Name;
    public CityConstructor.LoadState LoadState;
}

public class CustomStep
{
    public Step Step;
    public Step After;
    public Step Original = new();
}

[AttributeUsage(AttributeTargets.Method)]
public class StateAttribute : Attribute
{
    public string StateName { get; }

    public StateAttribute(string stateName)
    {
        StateName = stateName;
    }
}