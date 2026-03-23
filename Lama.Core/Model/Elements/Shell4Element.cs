using System.Collections.Generic;

namespace Lama.Core.Model.Elements
{
    /// <summary>
    /// Linear four-node quadrilateral shell.
    /// </summary>
    public sealed class Shell4Element : ElementBase
    {
        public override CalculixElementType ElementType => CalculixElementType.S4;

        public Shell4Element(int id, string elementSetName, IEnumerable<int> nodeIds)
            : base(id, elementSetName, nodeIds, expectedNodeCount: 4)
        {
        }
    }
}
