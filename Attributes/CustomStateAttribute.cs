using System;

namespace SODCustomStateInjectorMiami.Attributes
{
    /// <summary>
    ///     This attribute specifies a custom state that should be injected into the game.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]

    public class CustomStateAttribute : Attribute
    {
        /// <summary>
        ///  Adds a state to be added to the generation process.
        /// </summary>
        /// <param name="stepName"> The name of the step to be added.</param>
        /// <param name="afterStepName"> The name of the step that happens before this step.</param>
        public CustomStateAttribute(string stepName, string afterStepName)
        {
            StepName = stepName;
            AfterStepName = afterStepName;
        }

        public string StepName { get; }
        public string AfterStepName { get; }
    }

}