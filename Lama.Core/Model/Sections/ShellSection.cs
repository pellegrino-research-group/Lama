using System;
using System.Collections.Generic;
using Lama.Core.Materials;

namespace Lama.Core.Model.Sections
{
    /// <summary>
    /// Material and thickness assignment for shell element sets.
    /// </summary>
    public sealed class ShellSection : SectionBase
    {
        public double UniformThickness { get; set; }
        public IDictionary<int, double> ElementThickness { get; } = new Dictionary<int, double>();
        public IDictionary<int, double> NodeThickness { get; } = new Dictionary<int, double>();

        public ShellSection(string elementSetName, MaterialBase material, double uniformThickness)
            : base(elementSetName, material)
        {
            if (uniformThickness <= 0)
                throw new ArgumentOutOfRangeException(nameof(uniformThickness), "Shell thickness must be positive.");

            UniformThickness = uniformThickness;
        }
    }
}
