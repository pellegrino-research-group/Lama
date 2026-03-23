using System.Collections.Generic;

namespace Lama.Core.Model.Elements
{
    /// <summary>
    /// Quadratic eight-node quadrilateral shell (reduced integration).
    /// </summary>
    public sealed class Shell8Element : ElementBase
    {
        public override CalculixElementType ElementType => CalculixElementType.S8R;

        public Shell8Element(int id, string elementSetName, IEnumerable<int> nodeIds)
            : base(id, elementSetName, nodeIds, expectedNodeCount: 8)
        {
        }
    }
}
