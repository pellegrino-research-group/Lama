using System;
using System.Globalization;
using System.IO;
using Lama.Core.Model;
using Lama.Core.Model.Elements;

namespace Lama.Core.Meshing
{
    /// <summary>
    /// Parses Gmsh MSH v2.2 files into a <see cref="StructuralModel"/>.
    /// Only tetrahedral elements (4-node and 10-node) are extracted;
    /// lower-dimension entities (points, lines, triangles) are skipped.
    /// </summary>
    public static class GmshMshParser
    {
        private const int GmshTetra4Type = 4;
        private const int GmshTetra10Type = 11;
        private static readonly char[] Separators = { ' ', '\t' };

        public static StructuralModel Parse(string mshFilePath, string elementSetName = "E_TET")
        {
            if (string.IsNullOrWhiteSpace(mshFilePath))
                throw new ArgumentException("MSH file path cannot be empty.", nameof(mshFilePath));
            if (!File.Exists(mshFilePath))
                throw new FileNotFoundException("MSH file not found.", mshFilePath);
            if (string.IsNullOrWhiteSpace(elementSetName))
                throw new ArgumentException("Element set name cannot be empty.", nameof(elementSetName));

            var lines = File.ReadAllLines(mshFilePath);
            var model = new StructuralModel();

            var i = 0;
            while (i < lines.Length)
            {
                var line = lines[i].Trim();
                if (line == "$Nodes")
                {
                    i = ReadNodes(lines, i + 1, model);
                }
                else if (line == "$Elements")
                {
                    i = ReadElements(lines, i + 1, model, elementSetName);
                }
                else
                {
                    i++;
                }
            }

            if (model.Nodes.Count == 0)
                throw new InvalidOperationException("No nodes found in MSH file.");
            if (model.Elements.Count == 0)
                throw new InvalidOperationException("No tetrahedral elements found in MSH file.");

            return model;
        }

        private static int ReadNodes(string[] lines, int index, StructuralModel model)
        {
            var numNodes = int.Parse(lines[index].Trim(), CultureInfo.InvariantCulture);
            index++;

            for (var j = 0; j < numNodes; j++, index++)
            {
                var parts = lines[index].Trim().Split(Separators, StringSplitOptions.RemoveEmptyEntries);
                var id = int.Parse(parts[0], CultureInfo.InvariantCulture);
                var x = double.Parse(parts[1], CultureInfo.InvariantCulture);
                var y = double.Parse(parts[2], CultureInfo.InvariantCulture);
                var z = double.Parse(parts[3], CultureInfo.InvariantCulture);
                model.Nodes.Add(new Node(id, x, y, z));
            }

            // Skip $EndNodes line.
            return index + 1;
        }

        private static int ReadElements(string[] lines, int index, StructuralModel model, string elementSetName)
        {
            var numElements = int.Parse(lines[index].Trim(), CultureInfo.InvariantCulture);
            index++;

            var nextElemId = 1;
            for (var j = 0; j < numElements; j++, index++)
            {
                var parts = lines[index].Trim().Split(Separators, StringSplitOptions.RemoveEmptyEntries);
                var elemType = int.Parse(parts[1], CultureInfo.InvariantCulture);
                var numTags = int.Parse(parts[2], CultureInfo.InvariantCulture);
                var nodeStart = 3 + numTags;

                if (elemType == GmshTetra4Type)
                {
                    var nodeIds = ParseNodeIds(parts, nodeStart, 4);
                    model.Elements.Add(new Tetra4Element(nextElemId++, elementSetName, nodeIds));
                }
                else if (elemType == GmshTetra10Type)
                {
                    // Gmsh and CalculiX use the same 10-node tet node ordering:
                    // 4 corners followed by 6 mid-edge nodes (12,23,13,14,24,34).
                    var nodeIds = ParseNodeIds(parts, nodeStart, 10);
                    model.Elements.Add(new Tetra10Element(nextElemId++, elementSetName, nodeIds));
                }
                // Other element types (points, lines, triangles) are skipped.
            }

            // Skip $EndElements line.
            return index + 1;
        }

        private static int[] ParseNodeIds(string[] parts, int offset, int count)
        {
            var ids = new int[count];
            for (var k = 0; k < count; k++)
                ids[k] = int.Parse(parts[offset + k], CultureInfo.InvariantCulture);
            return ids;
        }
    }
}
