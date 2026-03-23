using System;
using System.Collections.Generic;
using System.Linq;

namespace Lama.Core.Model.Elements
{
    /// <summary>
    /// Base class for all finite elements.
    /// </summary>
    public abstract class ElementBase : IElement
    {
        public int Id { get; }
        public string ElementSetName { get; }
        public abstract CalculixElementType ElementType { get; }
        public IReadOnlyList<int> NodeIds { get; }

        protected ElementBase(int id, string elementSetName, IEnumerable<int> nodeIds, int expectedNodeCount)
        {
            if (id <= 0)
                throw new ArgumentOutOfRangeException(nameof(id), "Element id must be positive.");
            if (string.IsNullOrWhiteSpace(elementSetName))
                throw new ArgumentException("Element set name cannot be empty.", nameof(elementSetName));

            var nodes = nodeIds?.ToList() ?? throw new ArgumentNullException(nameof(nodeIds));
            if (nodes.Count != expectedNodeCount)
                throw new ArgumentException($"Expected {expectedNodeCount} node ids.", nameof(nodeIds));
            if (nodes.Any(n => n <= 0))
                throw new ArgumentException("Node ids must be positive.", nameof(nodeIds));

            Id = id;
            ElementSetName = elementSetName;
            NodeIds = nodes;
        }
    }
}
