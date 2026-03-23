namespace Lama.Core.Model.Steps
{
    /// <summary>
    /// Implicit time integration dynamic step.
    /// </summary>
    public sealed class DynamicImplicitStep : AnalysisStepBase
    {
        public double InitialIncrement { get; set; } = 0.01;
        public double TimePeriod { get; set; } = 1.0;
        public double MinimumIncrement { get; set; } = 1e-8;
        public double MaximumIncrement { get; set; } = 0.1;

        public DynamicImplicitStep(string name = "DynamicImplicit")
            : base(name)
        {
        }
    }
}
