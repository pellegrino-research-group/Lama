using System;
using System.Collections.Generic;
using Lama.Core.Model.Loads;

namespace Lama.Core.Model.Steps
{
    /// <summary>
    /// Base class for analysis step definitions.
    /// </summary>
    public abstract class AnalysisStepBase
    {
        public string Name { get; }
        public IList<NodalLoad> NodalLoads { get; } = new List<NodalLoad>();
        public IList<StepOutputRequest> OutputRequests { get; } = new List<StepOutputRequest>();

        protected AnalysisStepBase(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Step name cannot be empty.", nameof(name));

            Name = name;
        }
    }
}
