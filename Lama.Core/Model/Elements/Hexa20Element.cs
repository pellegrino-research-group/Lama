using System.Collections.Generic;

namespace Lama.Core.Model.Elements
{
    /// <summary>
    /// Quadratic twenty-node hexahedral solid (reduced integration).
    /// </summary>
    public sealed class Hexa20Element : ElementBase
    {
        public override CalculixElementType ElementType => CalculixElementType.C3D20R;

        public Hexa20Element(int id, string elementSetName, IEnumerable<int> nodeIds)
            : base(id, elementSetName, nodeIds, expectedNodeCount: 20)
        {
        }
    }
}
