using System.Collections.Generic;

namespace Lama.Core.Model.Elements
{
    /// <summary>
    /// Quadratic six-node triangular shell.
    /// </summary>
    public sealed class Shell6Element : ElementBase
    {
        public override CalculixElementType ElementType => CalculixElementType.S6;

        public Shell6Element(int id, string elementSetName, IEnumerable<int> nodeIds)
            : base(id, elementSetName, nodeIds, expectedNodeCount: 6)
        {
        }
    }
}
