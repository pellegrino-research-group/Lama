using System;

namespace Lama.Core.Model.Steps
{
    /// <summary>
    /// Eigenfrequency extraction step.
    /// </summary>
    public sealed class FrequencyStep : AnalysisStepBase
    {
        public int NumberOfModes { get; set; } = 10;

        public FrequencyStep(string name = "Frequency")
            : base(name)
        {
        }
    }
}
