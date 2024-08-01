using System;

namespace SODCustomStateInjectorMiami.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class CustomStateAttribute(string stepName, string afterStepName) : Attribute
    {
        public string StepName { get; } = stepName;
        public string AfterStepName { get; } = afterStepName;
    }

}