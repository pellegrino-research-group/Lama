using System.Collections.Generic;

namespace Lama.Core.Model.Elements
{
    /// <summary>
    /// Linear four-node tetrahedral solid.
    /// </summary>
    public sealed class Tetra4Element : ElementBase
    {
        public override CalculixElementType ElementType => CalculixElementType.C3D4;

        public Tetra4Element(int id, string elementSetName, IEnumerable<int> nodeIds)
            : base(id, elementSetName, nodeIds, expectedNodeCount: 4)
        {
        }
    }
}
