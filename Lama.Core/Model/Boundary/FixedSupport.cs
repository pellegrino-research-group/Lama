using System;
using System.Collections.Generic;
using System.Linq;

namespace Lama.Core.Model.Boundary
{
    /// <summary>
    /// Homogeneous displacement constraints for a node set.
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
        public bool FixTranslations { get; }
        public bool FixRotations { get; }

        public FixedSupport(string name, IEnumerable<int> nodeIds, bool fixTranslations = true, bool fixRotations = false)
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
            FixTranslations = fixTranslations;
            FixRotations = fixRotations;
        }

        public FixedSupport(string name, IEnumerable<SupportPointTarget> targetPoints, bool fixTranslations = true, bool fixRotations = false)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Support name cannot be empty.", nameof(name));

            var points = targetPoints?.Distinct().ToList() ?? throw new ArgumentNullException(nameof(targetPoints));
            if (points.Count == 0)
                throw new ArgumentException("Support must contain at least one target point.", nameof(targetPoints));

            Name = name;
            _nodeIds = null;
            TargetPoints = points;
            FixTranslations = fixTranslations;
            FixRotations = fixRotations;
        }

        public FixedSupport ResolveNodeIds(IEnumerable<int> nodeIds)
        {
            return new FixedSupport(Name, nodeIds, FixTranslations, FixRotations);
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
