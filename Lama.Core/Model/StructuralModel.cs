using System;
using System.Collections.Generic;
using System.Linq;
using Lama.Core.Materials;
using Lama.Core.Model.Boundary;
using Lama.Core.Model.Elements;
using Lama.Core.Model.Sections;
using Lama.Core.Model.Steps;

namespace Lama.Core.Model
{
    /// <summary>
    /// Single source of truth for FE model entities.
    /// </summary>
    public sealed class StructuralModel
    {
        public string Name { get; set; } = "LamaModel";
        public string Path { get; set; } = string.Empty;
        public IList<Node> Nodes { get; } = new List<Node>();
        public IList<IElement> Elements { get; } = new List<IElement>();
        public IList<MaterialBase> Materials { get; } = new List<MaterialBase>();
        public IList<SectionBase> Sections { get; } = new List<SectionBase>();
        public IList<FixedSupport> FixedSupports { get; } = new List<FixedSupport>();
        public IList<AnalysisStepBase> Steps { get; } = new List<AnalysisStepBase>();

        public void Validate()
        {
            ValidateUniqueNodeIds();
            ValidateUniqueElementIds();
            ValidateElementConnectivity();
            ValidateSections();
        }

        /// <summary>
        /// Ensures at least one analysis step is present. Required before running the solver;
        /// omitted from <see cref="Validate"/> so input decks can be generated for incomplete models.
        /// </summary>
        public void EnsureHasAnalysisSteps()
        {
            if (Steps.Count == 0)
                throw new InvalidOperationException(
                    "Model must contain at least one analysis step. Connect a step component (e.g., StaticStep) to StructuralModel.");
        }

        private void ValidateUniqueNodeIds()
        {
            if (Nodes.GroupBy(n => n.Id).Any(g => g.Count() > 1))
                throw new InvalidOperationException("Duplicate node ids detected.");
        }

        private void ValidateUniqueElementIds()
        {
            if (Elements.GroupBy(e => e.Id).Any(g => g.Count() > 1))
                throw new InvalidOperationException("Duplicate element ids detected.");
        }

        private void ValidateElementConnectivity()
        {
            var nodeIdSet = new HashSet<int>(Nodes.Select(n => n.Id));
            foreach (var element in Elements)
            {
                if (element.NodeIds.Any(nodeId => !nodeIdSet.Contains(nodeId)))
                    throw new InvalidOperationException($"Element {element.Id} references unknown node ids.");
            }
        }

        private void ValidateSections()
        {
            var elementSetNames = new HashSet<string>(Elements.Select(e => e.ElementSetName), StringComparer.OrdinalIgnoreCase);
            foreach (var section in Sections)
            {
                if (!elementSetNames.Contains(section.ElementSetName))
                    throw new InvalidOperationException($"Section references unknown element set '{section.ElementSetName}'.");
            }
        }

    }
}
