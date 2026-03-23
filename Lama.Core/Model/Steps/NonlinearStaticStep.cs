using System;

namespace Lama.Core.Model.Steps
{
    /// <summary>
    /// Geometrically nonlinear static analysis step.
    /// </summary>
    public sealed class NonlinearStaticStep : AnalysisStepBase
    {
        public double InitialIncrement { get; set; } = 1.0;
        public double TimePeriod { get; set; } = 1.0;
        public double MinimumIncrement { get; set; } = 1e-6;
        public double MaximumIncrement { get; set; } = 1.0;

        public NonlinearStaticStep(string name = "NonlinearStatic")
            : base(name)
        {
        }
    }
}
