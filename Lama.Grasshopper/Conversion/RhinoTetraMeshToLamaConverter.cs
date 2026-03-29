using System;
using System.Collections.Generic;
using System.Linq;
using Lama.Core.Model;
using Lama.Core.Model.Elements;
using Rhino.Geometry;

namespace Lama.Gh.Conversion
{
    /// <summary>
    /// Converts Rhino tetra meshes (4 corner vertices) into Lama Tetra10 elements.
    /// Midside nodes are generated on the 6 topological edges.
    /// Duplicate nodes are merged by coordinate tolerance.
    /// </summary>
    public static class RhinoTetraMeshToLamaConverter
    {
        private static readonly (int A, int B)[] Tetra10Edges =
        {
            (0, 1), (1, 2), (2, 0), (0, 3), (1, 3), (2, 3)
        };

        public static StructuralModel CreateModelFromTetraMeshes(
            IEnumerable<Mesh> meshes,
            double mergeTolerance,
            string modelName = "RhinoTetModel",
            string elementSetName = "E_TET")
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
                Name = string.IsNullOrWhiteSpace(modelName) ? "RhinoTetModel" : modelName
            };

            var nodeMap = new Dictionary<NodeKey, int>();
            var nextNodeId = 1;
            var nextElementId = 1;

            foreach (var mesh in meshList)
            {
                if (mesh.Vertices.Count != 4)
                    throw new ArgumentException("Each tetra mesh must contain exactly 4 vertices.");
                if (mesh.Faces.Count != 4)
                    throw new ArgumentException("Each tetra mesh must contain exactly 4 faces.");

                var corners = Enumerable.Range(0, 4)
                    .Select(i => ToPoint3d(mesh.Vertices[i]))
                    .ToArray();
                EnsurePositiveOrientation(corners);

                var nodeIds = new int[10];

                // Corner nodes 1..4.
                for (var i = 0; i < 4; i++)
                    nodeIds[i] = GetOrAddNode(model, nodeMap, corners[i], mergeTolerance, ref nextNodeId);

                // Midside nodes 5..10 following CalculiX C3D10 ordering.
                for (var i = 0; i < Tetra10Edges.Length; i++)
                {
                    var (a, b) = Tetra10Edges[i];
                    var mid = 0.5 * (corners[a] + corners[b]);
                    nodeIds[4 + i] = GetOrAddNode(model, nodeMap, mid, mergeTolerance, ref nextNodeId);
                }

                model.Elements.Add(new Tetra10Element(
                    id: nextElementId++,
                    elementSetName: elementSetName,
                    nodeIds: nodeIds));
            }

            return model;
        }

        private static void EnsurePositiveOrientation(IList<Point3d> corners)
        {
            var v = SignedSixVolume(corners[0], corners[1], corners[2], corners[3]);
            if (Math.Abs(v) < 1e-15)
                throw new ArgumentException("Degenerate tetrahedron: zero volume.");

            if (v > 0)
                return;

            // Swap nodes 2 and 3 to flip orientation.
            var tmp = corners[1];
            corners[1] = corners[2];
            corners[2] = tmp;
        }

        private static double SignedSixVolume(Point3d p1, Point3d p2, Point3d p3, Point3d p4)
        {
            var a = p2 - p1;
            var b = p3 - p1;
            var c = p4 - p1;
            return Vector3d.Multiply(Vector3d.CrossProduct(a, b), c);
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
