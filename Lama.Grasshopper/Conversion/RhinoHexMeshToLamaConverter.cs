using System;
using System.Collections.Generic;
using System.Linq;
using Lama.Core.Model;
using Lama.Core.Model.Elements;
using Rhino.Geometry;

namespace Lama.Gh.Conversion
{
    /// <summary>
    /// Converts Rhino hex meshes (8 corner vertices) into Lama Hexa20 elements.
    /// Midside nodes are generated on the 12 topological edges.
    /// Duplicate nodes are merged by coordinate tolerance.
    /// </summary>
    public static class RhinoHexMeshToLamaConverter
    {
        private static readonly (int A, int B)[] Hex20Edges =
        {
            (0, 1), (1, 2), (2, 3), (3, 0),
            (4, 5), (5, 6), (6, 7), (7, 4),
            (0, 4), (1, 5), (2, 6), (3, 7)
        };

        public static StructuralModel CreateModelFromHexMeshes(
            IEnumerable<Mesh> meshes,
            double mergeTolerance,
            string modelName = "RhinoHexModel",
            string elementSetName = "E_HEX")
        {
            if (meshes == null)
                throw new ArgumentNullException(nameof(meshes));
            if (mergeTolerance <= 0)
                throw new ArgumentOutOfRangeException(nameof(mergeTolerance), "Tolerance must be positive.");
            if (string.IsNullOrWhiteSpace(elementSetName))
                throw new ArgumentException("Element set name cannot be empty.", nameof(elementSetName));

            var meshList = meshes.Where(m => m != null).ToList();
            if (meshList.Count == 0)
                throw new ArgumentException("At least one mesh is required.", nameof(meshes));

            var model = new StructuralModel
            {
                Name = string.IsNullOrWhiteSpace(modelName) ? "RhinoHexModel" : modelName
            };

            var nodeMap = new Dictionary<NodeKey, int>();
            var nextNodeId = 1;
            var nextElementId = 1;

            foreach (var mesh in meshList)
            {
                if (mesh.Vertices.Count != 8)
                    throw new ArgumentException("Each hex mesh must contain exactly 8 vertices.");
                if (mesh.Faces.Count != 6)
                    throw new ArgumentException("Each hex mesh must contain exactly 6 faces.");

                var corners = Enumerable.Range(0, 8)
                    .Select(i => ToPoint3d(mesh.Vertices[i]))
                    .ToArray();

                var nodeIds = new int[20];

                // Corner nodes 1..8.
                for (var i = 0; i < 8; i++)
                    nodeIds[i] = GetOrAddNode(model, nodeMap, corners[i], mergeTolerance, ref nextNodeId);

                // Midside nodes 9..20 following CalculiX C3D20R ordering.
                for (var i = 0; i < Hex20Edges.Length; i++)
                {
                    var (a, b) = Hex20Edges[i];
                    var mid = 0.5 * (corners[a] + corners[b]);
                    nodeIds[8 + i] = GetOrAddNode(model, nodeMap, mid, mergeTolerance, ref nextNodeId);
                }

                model.Elements.Add(new Hexa20Element(
                    id: nextElementId++,
                    elementSetName: elementSetName,
                    nodeIds: nodeIds));
            }

            return model;
        }

        private static Point3d ToPoint3d(Point3f p) => new Point3d(p.X, p.Y, p.Z);

        private static int GetOrAddNode(
            StructuralModel model,
            IDictionary<NodeKey, int> nodeMap,
            Point3d point,
            double tolerance,
            ref int nextNodeId)
        {
            var key = NodeKey.FromPoint(point, tolerance);
            if (nodeMap.TryGetValue(key, out var existing))
                return existing;

            var id = nextNodeId++;
            nodeMap.Add(key, id);
            model.Nodes.Add(new Node(id, point.X, point.Y, point.Z));
            return id;
        }

        private readonly struct NodeKey : IEquatable<NodeKey>
        {
            private readonly long _x;
            private readonly long _y;
            private readonly long _z;

            private NodeKey(long x, long y, long z)
            {
                _x = x;
                _y = y;
                _z = z;
            }

            public static NodeKey FromPoint(Point3d point, double tolerance)
            {
                var inv = 1.0 / tolerance;
                return new NodeKey(
                    x: (long)Math.Round(point.X * inv, MidpointRounding.AwayFromZero),
                    y: (long)Math.Round(point.Y * inv, MidpointRounding.AwayFromZero),
                    z: (long)Math.Round(point.Z * inv, MidpointRounding.AwayFromZero));
            }

            public bool Equals(NodeKey other) => _x == other._x && _y == other._y && _z == other._z;
            public override bool Equals(object obj) => obj is NodeKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = _x.GetHashCode();
                    hash = (hash * 397) ^ _y.GetHashCode();
                    hash = (hash * 397) ^ _z.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
