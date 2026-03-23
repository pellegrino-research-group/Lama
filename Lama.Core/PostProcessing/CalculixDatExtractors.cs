using System;
using System.Collections.Generic;
using System.Linq;

namespace Lama.Core.PostProcessing
{
    /// <summary>
    /// Typed nodal vector result extracted from a CalculiX .dat table.
    /// </summary>
    public sealed class NodalVectorResult
    {
        public int NodeId { get; }
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public NodalVectorResult(int nodeId, double x, double y, double z)
        {
            NodeId = nodeId;
            X = x;
            Y = y;
            Z = z;
        }
    }

    /// <summary>
    /// Typed element stress result extracted from a CalculiX .dat table.
    /// </summary>
    public sealed class ElementStressResult
    {
        public int ElementId { get; }
        public IReadOnlyList<double> Components { get; }

        public ElementStressResult(int elementId, IReadOnlyList<double> components)
        {
            ElementId = elementId;
            Components = components ?? throw new ArgumentNullException(nameof(components));
        }
    }

    /// <summary>
    /// Convenience extractors for common structural results from CalculiX .dat tables.
    /// </summary>
    public static class CalculixDatExtractors
    {
        public static bool TryGetNodalDisplacements(
            IEnumerable<CalculixDatTable> tables,
            out IReadOnlyList<NodalVectorResult> displacements)
        {
            return TryGetNodalVectorByKeyword(tables, "displacement", out displacements);
        }

        public static bool TryGetNodalReactions(
            IEnumerable<CalculixDatTable> tables,
            out IReadOnlyList<NodalVectorResult> reactions)
        {
            return TryGetNodalVectorByKeyword(tables, "reaction", out reactions);
        }

        public static bool TryGetElementStress(
            IEnumerable<CalculixDatTable> tables,
            out IReadOnlyList<ElementStressResult> stresses)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));

            var stressTable = SelectBestTableByKeyword(tables, "stress");
            if (stressTable == null)
            {
                stresses = Array.Empty<ElementStressResult>();
                return false;
            }

            stresses = stressTable.Rows
                .Where(r => r.Values.Count > 0)
                .Select(r => new ElementStressResult(r.EntityId, r.Values.ToArray()))
                .ToList();

            return stresses.Count > 0;
        }

        private static bool TryGetNodalVectorByKeyword(
            IEnumerable<CalculixDatTable> tables,
            string keyword,
            out IReadOnlyList<NodalVectorResult> vectors)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));

            var table = SelectBestTableByKeyword(tables, keyword);
            if (table == null)
            {
                vectors = Array.Empty<NodalVectorResult>();
                return false;
            }

            vectors = table.Rows
                .Where(r => r.Values.Count >= 3)
                .Select(r => new NodalVectorResult(r.EntityId, r.Values[0], r.Values[1], r.Values[2]))
                .ToList();

            return vectors.Count > 0;
        }

        private static CalculixDatTable SelectBestTableByKeyword(IEnumerable<CalculixDatTable> tables, string keyword)
        {
            return CalculixDatParser
                .FindTablesByHeaderKeyword(tables, keyword)
                .OrderByDescending(t => t.Rows.Count)
                .ThenByDescending(t => t.Rows.Count == 0 ? 0 : t.Rows[0].Values.Count)
                .FirstOrDefault();
        }
    }
}
