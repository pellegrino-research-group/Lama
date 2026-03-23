using System;

namespace Lama.Core.Model.Loads
{
    /// <summary>
    /// Concentrated nodal force or moment.
    /// </summary>
    public sealed class NodalLoad
    {
        private readonly int? _nodeId;

        public int NodeId
        {
            get
            {
                if (!_nodeId.HasValue)
                    throw new InvalidOperationException("Nodal load target node id is unresolved.");

                return _nodeId.Value;
            }
        }

        public bool HasNodeId => _nodeId.HasValue;
        public double? X { get; }
        public double? Y { get; }
        public double? Z { get; }
        public StructuralDof Dof { get; }
        public double Value { get; }

        public NodalLoad(int nodeId, StructuralDof dof, double value)
        {
            if (nodeId <= 0)
                throw new ArgumentOutOfRangeException(nameof(nodeId), "Node id must be positive.");

            _nodeId = nodeId;
            Dof = dof;
            Value = value;
        }

        public NodalLoad(double x, double y, double z, StructuralDof dof, double value)
        {
            X = x;
            Y = y;
            Z = z;
            Dof = dof;
            Value = value;
        }

        public NodalLoad ResolveNodeId(int nodeId)
        {
            return new NodalLoad(nodeId, Dof, Value);
        }
    }
}
