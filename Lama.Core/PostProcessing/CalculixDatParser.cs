using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Lama.Core.PostProcessing
{
    /// <summary>
    /// One numeric row found in a CalculiX .dat table.
    /// </summary>
    public sealed class CalculixDatRow
    {
        public int EntityId { get; }
        public IReadOnlyList<double> Values { get; }
        public string RawLine { get; }

        public CalculixDatRow(int entityId, IReadOnlyList<double> values, string rawLine)
        {
            EntityId = entityId;
            Values = values ?? throw new ArgumentNullException(nameof(values));
            RawLine = rawLine ?? string.Empty;
        }
    }

    /// <summary>
    /// Numeric block extracted from a CalculiX .dat file.
    /// </summary>
    public sealed class CalculixDatTable
    {
        public string Header { get; }
        public IReadOnlyList<string> HeaderLines { get; }
        public IReadOnlyList<CalculixDatRow> Rows { get; }

        public CalculixDatTable(string header, IReadOnlyList<string> headerLines, IReadOnlyList<CalculixDatRow> rows)
        {
            Header = header ?? string.Empty;
            HeaderLines = headerLines ?? Array.Empty<string>();
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
        }
    }

    /// <summary>
    /// Lightweight parser for extracting numeric result blocks from CalculiX .dat output.
    /// </summary>
    public static class CalculixDatParser
    {
        public static IReadOnlyList<CalculixDatTable> ParseFile(string datPath)
        {
            if (string.IsNullOrWhiteSpace(datPath))
                throw new ArgumentException("Path cannot be empty.", nameof(datPath));
            if (!File.Exists(datPath))
                throw new FileNotFoundException("DAT file not found.", datPath);

            return ParseText(File.ReadAllText(datPath));
        }

        public static IReadOnlyList<CalculixDatTable> ParseText(string datText)
        {
            if (datText == null)
                throw new ArgumentNullException(nameof(datText));

            var lines = datText.Replace("\r\n", "\n").Split('\n');
            var tables = new List<CalculixDatTable>();
            var headerWindow = new List<string>();

            List<CalculixDatRow> currentRows = null;
            List<string> currentHeaderLines = null;

            foreach (var rawLine in lines)
            {
                if (TryParseNumericRow(rawLine, out var entityId, out var values))
                {
                    if (currentRows == null)
                    {
                        currentRows = new List<CalculixDatRow>();
                        currentHeaderLines = headerWindow.ToList();
                    }

                    currentRows.Add(new CalculixDatRow(entityId, values, rawLine));
                    continue;
                }

                if (currentRows != null && currentRows.Count > 0)
                {
                    AddTable(tables, currentHeaderLines, currentRows);
                    currentRows = null;
                    currentHeaderLines = null;
                    headerWindow.Clear();
                }

                if (!string.IsNullOrWhiteSpace(rawLine))
                {
                    headerWindow.Add(rawLine.Trim());
                    if (headerWindow.Count > 8)
                        headerWindow.RemoveAt(0);
                }
            }

            if (currentRows != null && currentRows.Count > 0)
                AddTable(tables, currentHeaderLines, currentRows);

            return tables;
        }

        public static IReadOnlyList<CalculixDatTable> FindTablesByHeaderKeyword(
            IEnumerable<CalculixDatTable> tables,
            string keyword)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));
            if (string.IsNullOrWhiteSpace(keyword))
                throw new ArgumentException("Keyword cannot be empty.", nameof(keyword));

            return tables
                .Where(t => t.HeaderLines.Any(h => h.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();
        }

        private static void AddTable(
            IList<CalculixDatTable> tables,
            IReadOnlyList<string> headerLines,
            IReadOnlyList<CalculixDatRow> rows)
        {
            var safeHeaderLines = (headerLines ?? Array.Empty<string>()).Where(h => !string.IsNullOrWhiteSpace(h)).ToList();
            var header = safeHeaderLines.Count == 0 ? string.Empty : safeHeaderLines[safeHeaderLines.Count - 1];
            tables.Add(new CalculixDatTable(header, safeHeaderLines, rows));
        }

        private static bool TryParseNumericRow(string line, out int entityId, out IReadOnlyList<double> values)
        {
            entityId = default;
            values = Array.Empty<double>();

            if (string.IsNullOrWhiteSpace(line))
                return false;

            var tokens = line
                .Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length < 2)
                return false;

            if (!int.TryParse(tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out entityId))
                return false;

            var parsedValues = new List<double>(tokens.Length - 1);
            for (var i = 1; i < tokens.Length; i++)
            {
                var normalized = tokens[i].Replace('D', 'E').Replace('d', 'e');
                if (!double.TryParse(normalized, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value))
                    return false;
                parsedValues.Add(value);
            }

            values = parsedValues;
            return true;
        }
    }
}
