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
        public GravityLoad GravityLoad { get; set; }
        public IList<StepOutputRequest> OutputRequests { get; } = new List<StepOutputRequest>();

        /// <summary>
        /// When true (default), loads from previous steps carry over (OP=MOD).
        /// When false, previous loads are cleared and only this step's loads apply (OP=NEW).
        /// </summary>
        public bool PropagateLoads { get; set; } = true;

        protected AnalysisStepBase(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Step name cannot be empty.", nameof(name));

            Name = name;
        }
    }
}
