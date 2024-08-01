using System;

namespace SODCustomStateInjectorMiami.Attributes
{

    [AttributeUsage(AttributeTargets.Method)]
    public class GenerateStateAttribute(string stateName) : Attribute
    {
        public string StateName { get; } = stateName;
    }
}