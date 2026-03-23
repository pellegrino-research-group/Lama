using System.Collections.Generic;

namespace Lama.Core.Model.Elements
{
    /// <summary>
    /// Linear three-node triangular shell.
    /// </summary>
    public sealed class Shell3Element : ElementBase
    {
        public override CalculixElementType ElementType => CalculixElementType.S3;

        public Shell3Element(int id, string elementSetName, IEnumerable<int> nodeIds)
            : base(id, elementSetName, nodeIds, expectedNodeCount: 3)
        {
        }
    }
}
