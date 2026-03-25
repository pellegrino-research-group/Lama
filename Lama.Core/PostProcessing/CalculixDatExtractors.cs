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
    /// Nodal reaction from a CalculiX <c>*NODE PRINT</c> RF block: forces (RF1–RF3) and optional moments (RF4–RF6).
    /// </summary>
    public sealed class NodalReactionResult
    {
        public int NodeId { get; }
        public double Fx { get; }
        public double Fy { get; }
        public double Fz { get; }
        public double Mx { get; }
        public double My { get; }
        public double Mz { get; }

        public NodalReactionResult(int nodeId, double fx, double fy, double fz, double mx, double my, double mz)
        {
            NodeId = nodeId;
            Fx = fx;
            Fy = fy;
            Fz = fz;
            Mx = mx;
            My = my;
            Mz = mz;
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
            out IReadOnlyList<NodalReactionResult> reactions)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));

            var table = SelectBestReactionTable(tables);
            if (table == null)
            {
                reactions = Array.Empty<NodalReactionResult>();
                return false;
            }

            var list = new List<NodalReactionResult>();
            foreach (var row in table.Rows)
            {
                if (row.Values.Count < 3)
                    continue;
                if (row.Values.Count >= 6)
                {
                    list.Add(new NodalReactionResult(
                        row.EntityId,
                        row.Values[0], row.Values[1], row.Values[2],
                        row.Values[3], row.Values[4], row.Values[5]));
                }
                else
                {
                    list.Add(new NodalReactionResult(
                        row.EntityId,
                        row.Values[0], row.Values[1], row.Values[2],
                        0.0, 0.0, 0.0));
                }
            }

            reactions = list;
            return list.Count > 0;
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

        private static CalculixDatTable SelectBestReactionTable(IEnumerable<CalculixDatTable> tables)
        {
            var fromReaction = CalculixDatParser.FindTablesByHeaderKeyword(tables, "reaction").ToList();
            IEnumerable<CalculixDatTable> candidates = fromReaction.Count > 0
                ? fromReaction
                : tables.Where(t => t.HeaderLines.Any(h =>
                    h.IndexOf("rf1", StringComparison.OrdinalIgnoreCase) >= 0));

            return candidates
                .OrderByDescending(t => t.Rows.Count)
                .ThenByDescending(t => t.Rows.Count == 0
                    ? 0
                    : t.Rows.Max(r => r.Values.Count))
                .FirstOrDefault();
        }
    }
}
