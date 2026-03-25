using System;
using System.Collections.Generic;
using System.Linq;

namespace Lama.Core.Model.Boundary
{
    /// <summary>
    /// Homogeneous displacement constraints for a node set (CalculiX *BOUNDARY DOFs 1–6).
    /// </summary>
    public sealed class FixedSupport
    {
        private readonly IReadOnlyList<int> _nodeIds;

        public string Name { get; }
        public IReadOnlyList<int> NodeIds
        {
            get
            {
                if (!HasNodeIds)
                    throw new InvalidOperationException("Support target node ids are unresolved.");

                return _nodeIds;
            }
        }
        public bool HasNodeIds => _nodeIds != null;
        public IReadOnlyList<SupportPointTarget> TargetPoints { get; }

        /// <summary>Fix translational DOF 1 (Ux).</summary>
        public bool FixUx { get; }
        /// <summary>Fix translational DOF 2 (Uy).</summary>
        public bool FixUy { get; }
        /// <summary>Fix translational DOF 3 (Uz).</summary>
        public bool FixUz { get; }
        /// <summary>Fix rotational DOF 4 (Rx).</summary>
        public bool FixRx { get; }
        /// <summary>Fix rotational DOF 5 (Ry).</summary>
        public bool FixRy { get; }
        /// <summary>Fix rotational DOF 6 (Rz).</summary>
        public bool FixRz { get; }

        public bool IsDofFixed(int dof1To6)
        {
            if (dof1To6 < 1 || dof1To6 > 6)
                throw new ArgumentOutOfRangeException(nameof(dof1To6));

            return dof1To6 switch
            {
                1 => FixUx,
                2 => FixUy,
                3 => FixUz,
                4 => FixRx,
                5 => FixRy,
                6 => FixRz,
                _ => false
            };
        }

        public FixedSupport(
            string name,
            IEnumerable<int> nodeIds,
            bool fixUx,
            bool fixUy,
            bool fixUz,
            bool fixRx,
            bool fixRy,
            bool fixRz)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Support name cannot be empty.", nameof(name));

            var ids = nodeIds?.Distinct().ToList() ?? throw new ArgumentNullException(nameof(nodeIds));
            if (ids.Count == 0)
                throw new ArgumentException("Support must contain at least one node.", nameof(nodeIds));
            if (ids.Any(n => n <= 0))
                throw new ArgumentException("Node ids must be positive.", nameof(nodeIds));

            Name = name;
            _nodeIds = ids;
            TargetPoints = Array.Empty<SupportPointTarget>();
            FixUx = fixUx;
            FixUy = fixUy;
            FixUz = fixUz;
            FixRx = fixRx;
            FixRy = fixRy;
            FixRz = fixRz;
        }

        public FixedSupport(
            string name,
            IEnumerable<SupportPointTarget> targetPoints,
            bool fixUx,
            bool fixUy,
            bool fixUz,
            bool fixRx,
            bool fixRy,
            bool fixRz)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Support name cannot be empty.", nameof(name));

            var points = targetPoints?.Distinct().ToList() ?? throw new ArgumentNullException(nameof(targetPoints));
            if (points.Count == 0)
                throw new ArgumentException("Support must contain at least one target point.", nameof(targetPoints));

            Name = name;
            _nodeIds = null;
            TargetPoints = points;
            FixUx = fixUx;
            FixUy = fixUy;
            FixUz = fixUz;
            FixRx = fixRx;
            FixRy = fixRy;
            FixRz = fixRz;
        }

        public FixedSupport ResolveNodeIds(IEnumerable<int> nodeIds)
        {
            return new FixedSupport(Name, nodeIds, FixUx, FixUy, FixUz, FixRx, FixRy, FixRz);
        }

        public readonly struct SupportPointTarget : IEquatable<SupportPointTarget>
        {
            public SupportPointTarget(double x, double y, double z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public double X { get; }
            public double Y { get; }
            public double Z { get; }

            public bool Equals(SupportPointTarget other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
            public override bool Equals(object obj) => obj is SupportPointTarget other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = X.GetHashCode();
                    hash = (hash * 397) ^ Y.GetHashCode();
                    hash = (hash * 397) ^ Z.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
