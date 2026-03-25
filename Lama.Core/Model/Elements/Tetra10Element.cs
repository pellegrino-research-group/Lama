using System.Collections.Generic;

namespace Lama.Core.Model.Elements
{
    /// <summary>
    /// Quadratic ten-node tetrahedral solid (CalculiX TYPE=C3D10).
    /// </summary>
    public sealed class Tetra10Element : ElementBase
    {
        public override CalculixElementType ElementType => CalculixElementType.C3D10;

        public Tetra10Element(int id, string elementSetName, IEnumerable<int> nodeIds)
            : base(id, elementSetName, nodeIds, expectedNodeCount: 10)
        {
        }
    }
}
