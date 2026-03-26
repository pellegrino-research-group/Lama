using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Lama.Core.Materials;
using Lama.Core.Model;
using Lama.Core.Model.Boundary;
using Lama.Core.Model.Elements;
using Lama.Core.Model.Loads;
using Lama.Core.Model.Sections;
using Lama.Core.Model.Steps;

namespace Lama.Core.InputDeck
{
    /// <summary>
    /// Reads CalculiX/Abaqus-style .inp text into a <see cref="StructuralModel"/>.
    /// Supports the same keyword subset as <see cref="CalculixInputDeckBuilder"/>; other lines produce warnings.
    /// </summary>
    public sealed class CalculixInputDeckReader
    {
        private readonly List<string> _warnings = new List<string>();

        /// <summary>
        /// Reads an .inp file from disk. Warnings list parser remarks (unsupported keywords, skipped lines).
        /// </summary>
        public static (StructuralModel Model, IReadOnlyList<string> Warnings) ReadFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty.", nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Input deck file not found.", filePath);

            var text = File.ReadAllText(filePath);
            var reader = new CalculixInputDeckReader();
            var model = reader.Parse(text, Path.GetFileName(filePath));
            model.Path = Path.GetFullPath(filePath);
            return (model, reader._warnings.ToArray());
        }

        /// <summary>
        /// Parses .inp content (e.g. for tests). <paramref name="sourceName"/> appears in warnings.
        /// </summary>
        public static (StructuralModel Model, IReadOnlyList<string> Warnings) ReadFromText(string inpText, string sourceName = "input")
        {
            var reader = new CalculixInputDeckReader();
            var model = reader.Parse(inpText ?? string.Empty, sourceName ?? "input");
            return (model, reader._warnings.ToArray());
        }

        private void Warn(int line, string message) =>
            _warnings.Add($"{sourceLabel} line {line}: {message}");

        private string sourceLabel = "inp";

        private StructuralModel Parse(string text, string label)
        {
            sourceLabel = label;
            var model = new StructuralModel { Name = Path.GetFileNameWithoutExtension(label) };

            var lines = PreprocessLines(text);
            var segments = Tokenize(lines);
            ProcessSegments(model, segments);
            return model;
        }

        private static List<(int LineNumber, string Text)> PreprocessLines(string text)
        {
            var list = new List<(int, string)>();
            var lineNo = 0;
            using var reader = new StringReader(text);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                lineNo++;
                var t = line.Trim();
                if (t.Length == 0)
                    continue;
                if (t.StartsWith("**", StringComparison.Ordinal))
                    continue;
                list.Add((lineNo, line));
            }

            return list;
        }

        private sealed class RawSegment
        {
            public int LineNumber;
            public string Keyword = "";
            public Dictionary<string, string> Params = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public List<string> DataLines = new List<string>();
        }

        private List<RawSegment> Tokenize(List<(int LineNumber, string Text)> lines)
        {
            var segments = new List<RawSegment>();
            for (var i = 0; i < lines.Count; i++)
            {
                var (no, text) = lines[i];
                var trimmed = text.TrimStart();
                if (!trimmed.StartsWith("*", StringComparison.Ordinal))
                {
                    Warn(no, "Data line outside any * keyword block; ignored.");
                    continue;
                }

                if (!TryParseKeywordLine(trimmed, no, out var seg))
                    continue;

                i++;
                while (i < lines.Count)
                {
                    var (_, nextText) = lines[i];
                    var nt = nextText.TrimStart();
                    if (nt.StartsWith("*", StringComparison.Ordinal))
                        break;
                    seg.DataLines.Add(nextText);
                    i++;
                }

                i--;
                segments.Add(seg);
            }

            return segments;
        }

        private bool TryParseKeywordLine(string line, int lineNo, out RawSegment seg)
        {
            seg = new RawSegment { LineNumber = lineNo };
            var afterStar = line.Substring(1).TrimStart();
            if (afterStar.Length == 0)
            {
                Warn(lineNo, "Empty * keyword line; ignored.");
                return false;
            }

            var parts = SplitTopLevelCommas(afterStar);
            if (parts.Count == 0)
                return false;

            seg.Keyword = NormalizeKeyword(parts[0]);
            for (var p = 1; p < parts.Count; p++)
            {
                var token = parts[p].Trim();
                if (token.Length == 0)
                    continue;
                var eq = token.IndexOf('=');
                if (eq >= 0)
                {
                    var k = token.Substring(0, eq).Trim();
                    var v = token.Substring(eq + 1).Trim();
                    seg.Params[k.ToUpperInvariant()] = v;
                }
                else
                    seg.Params[token.ToUpperInvariant()] = "";
            }

            return true;
        }

        private static string NormalizeKeyword(string firstPart)
        {
            var s = firstPart.Trim().ToUpperInvariant();
            while (s.Contains("  ", StringComparison.Ordinal))
                s = s.Replace("  ", " ", StringComparison.Ordinal);
            return s;
        }

        private static List<string> SplitTopLevelCommas(string s)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            foreach (var c in s)
            {
                if (c == ',')
                {
                    list.Add(sb.ToString());
                    sb.Clear();
                }
                else
                    sb.Append(c);
            }

            list.Add(sb.ToString());
            return list;
        }

        private void ProcessSegments(StructuralModel model, List<RawSegment> segments)
        {
            var nsets = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            var elsets = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            var materialsByName = new Dictionary<string, MaterialBase>(StringComparer.OrdinalIgnoreCase);
            var orientations = new Dictionary<string, SectionOrientation>(StringComparer.OrdinalIgnoreCase);

            MaterialBase pendingMaterial = null;
            var stepIndex = 0;

            for (var i = 0; i < segments.Count; i++)
            {
                var s = segments[i];
                switch (s.Keyword)
                {
                    case "HEADING":
                        if (s.DataLines.Count > 0)
                        {
                            var name = s.DataLines[0].Trim();
                            if (!string.IsNullOrEmpty(name))
                                model.Name = name;
                        }

                        break;

                    case "NODE":
                        ParseNodes(model, s);
                        break;

                    case "NSET":
                        ParseNset(nsets, s);
                        break;

                    case "ELSET":
                        ParseElset(elsets, s);
                        break;

                    case "ELEMENT":
                        ParseElements(model, s);
                        break;

                    case "MATERIAL":
                        FlushMaterial(model, materialsByName, ref pendingMaterial);
                        if (!s.Params.TryGetValue("NAME", out var matName) || string.IsNullOrWhiteSpace(matName))
                        {
                            Warn(s.LineNumber, "*MATERIAL without NAME=; skipped.");
                            break;
                        }

                        pendingMaterial = new IsotropicMaterial(matName.Trim());
                        break;

                    case "ELASTIC":
                        ParseElastic(s, ref pendingMaterial);
                        break;

                    case "DENSITY":
                        ParseDensity(s, ref pendingMaterial);
                        break;

                    case "PLASTIC":
                        ParsePlastic(s, ref pendingMaterial);
                        break;

                    case "ORIENTATION":
                        ParseOrientation(orientations, s);
                        break;

                    case "SOLID SECTION":
                        FlushMaterial(model, materialsByName, ref pendingMaterial);
                        ParseSolidSection(model, materialsByName, orientations, s);
                        break;

                    case "SHELL SECTION":
                        FlushMaterial(model, materialsByName, ref pendingMaterial);
                        ParseShellSection(model, materialsByName, s);
                        break;

                    case "BOUNDARY":
                        ParseBoundary(model, nsets, s);
                        break;

                    case "STEP":
                        FlushMaterial(model, materialsByName, ref pendingMaterial);
                        stepIndex++;
                        if (!TryExtractStepBody(segments, ref i, s, out var body, out var nlgeom))
                        {
                            // Skip remainder so inner keywords are not parsed at file level
                            i = segments.Count;
                            break;
                        }

                        var step = BuildStep(stepIndex, body, nlgeom, s.LineNumber);
                        if (step != null)
                            model.Steps.Add(step);
                        break;

                    case "END STEP":
                        Warn(s.LineNumber, "*END STEP without matching *STEP; ignored.");
                        break;

                    default:
                        Warn(s.LineNumber, $"Unsupported keyword *{s.Keyword}; block skipped.");
                        break;
                }
            }

            FlushMaterial(model, materialsByName, ref pendingMaterial);
        }

        private void FlushMaterial(
            StructuralModel model,
            Dictionary<string, MaterialBase> byName,
            ref MaterialBase pending)
        {
            if (pending == null)
                return;

            // Replace prior material with same name (last *MATERIAL block wins)
            for (var k = model.Materials.Count - 1; k >= 0; k--)
            {
                if (string.Equals(model.Materials[k].Name, pending.Name, StringComparison.OrdinalIgnoreCase))
                    model.Materials.RemoveAt(k);
            }

            byName[pending.Name] = pending;
            model.Materials.Add(pending);
            pending = null;
        }

        private void ParseNodes(StructuralModel model, RawSegment s)
        {
            var records = JoinContinuationRecords(s.DataLines);
            foreach (var record in records)
            {
                var fields = SplitNumericFields(record);
                if (fields.Count < 2)
                    continue;
                if (!int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                {
                    Warn(s.LineNumber, $"Invalid node id '{fields[0]}'; line skipped.");
                    continue;
                }

                if (!double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
                {
                    Warn(s.LineNumber, $"Invalid node coordinate; line skipped.");
                    continue;
                }

                double y = 0, z = 0;
                if (fields.Count >= 4)
                {
                    double.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                    double.TryParse(fields[3], NumberStyles.Float, CultureInfo.InvariantCulture, out z);
                }
                else if (fields.Count == 3)
                {
                    double.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                    Warn(s.LineNumber, "2D node (id,x,y); z assumed 0.");
                }

                if (model.Nodes.Any(n => n.Id == id))
                {
                    Warn(s.LineNumber, $"Duplicate node id {id}; first definition kept.");
                    continue;
                }

                model.Nodes.Add(new Node(id, x, y, z));
            }
        }

        private void ParseNset(Dictionary<string, List<int>> nsets, RawSegment s)
        {
            if (!s.Params.TryGetValue("NSET", out var setName) || string.IsNullOrWhiteSpace(setName))
            {
                Warn(s.LineNumber, "*NSET without NSET=; skipped.");
                return;
            }

            setName = setName.Trim();
            if (!nsets.TryGetValue(setName, out var list))
            {
                list = new List<int>();
                nsets[setName] = list;
            }

            if (s.Params.ContainsKey("GENERATE"))
            {
                foreach (var record in JoinContinuationRecords(s.DataLines))
                {
                    var f = SplitNumericFields(record);
                    if (f.Count < 2)
                        continue;
                    if (!int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var a) ||
                        !int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var b))
                        continue;
                    var inc = 1;
                    if (f.Count >= 3)
                        int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out inc);
                    if (inc <= 0)
                        inc = 1;
                    if (a <= b)
                        for (var k = a; k <= b; k += inc)
                            list.Add(k);
                    else
                        for (var k = a; k >= b; k -= inc)
                            list.Add(k);
                }
            }
            else
                AddIntegersFromRecords(list, s.DataLines, s.LineNumber);
        }

        private void ParseElset(Dictionary<string, List<int>> elsets, RawSegment s)
        {
            if (!s.Params.TryGetValue("ELSET", out var setName) || string.IsNullOrWhiteSpace(setName))
            {
                Warn(s.LineNumber, "*ELSET without ELSET=; skipped.");
                return;
            }

            setName = setName.Trim();
            if (!elsets.TryGetValue(setName, out var list))
            {
                list = new List<int>();
                elsets[setName] = list;
            }

            if (s.Params.ContainsKey("GENERATE"))
            {
                foreach (var record in JoinContinuationRecords(s.DataLines))
                {
                    var f = SplitNumericFields(record);
                    if (f.Count < 2)
                        continue;
                    if (!int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var a) ||
                        !int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var b))
                        continue;
                    var inc = 1;
                    if (f.Count >= 3)
                        int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out inc);
                    if (inc <= 0)
                        inc = 1;
                    if (a <= b)
                        for (var k = a; k <= b; k += inc)
                            list.Add(k);
                    else
                        for (var k = a; k >= b; k -= inc)
                            list.Add(k);
                }
            }
            else
                AddIntegersFromRecords(list, s.DataLines, s.LineNumber);
        }

        private void AddIntegersFromRecords(List<int> target, List<string> dataLines, int lineNo)
        {
            foreach (var record in JoinContinuationRecords(dataLines))
            {
                foreach (var field in SplitNumericFields(record))
                {
                    if (int.TryParse(field, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                        target.Add(id);
                    else if (!string.IsNullOrWhiteSpace(field))
                        Warn(lineNo, $"Non-integer in set list '{field}'; skipped.");
                }
            }
        }

        private void ParseElements(StructuralModel model, RawSegment s)
        {
            if (!s.Params.TryGetValue("TYPE", out var typeStr) || string.IsNullOrWhiteSpace(typeStr))
            {
                Warn(s.LineNumber, "*ELEMENT without TYPE=; skipped.");
                return;
            }

            typeStr = typeStr.Trim().ToUpperInvariant();
            if (!s.Params.TryGetValue("ELSET", out var elset) || string.IsNullOrWhiteSpace(elset))
            {
                Warn(s.LineNumber, "*ELEMENT without ELSET=; skipped.");
                return;
            }

            elset = elset.Trim();
            if (!TryGetNodeCount(typeStr, out var nNodes))
            {
                Warn(s.LineNumber, $"Element type '{typeStr}' is not supported by Lama; block skipped.");
                return;
            }

            var records = JoinContinuationRecords(s.DataLines);
            foreach (var record in records)
            {
                var fields = SplitNumericFields(record);
                if (fields.Count < 1 + nNodes)
                {
                    Warn(s.LineNumber, "Incomplete *ELEMENT connectivity line; skipped.");
                    continue;
                }

                if (!int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var eid))
                    continue;

                var nodeIds = new int[nNodes];
                for (var k = 0; k < nNodes; k++)
                {
                    if (!int.TryParse(fields[1 + k], NumberStyles.Integer, CultureInfo.InvariantCulture, out nodeIds[k]))
                    {
                        Warn(s.LineNumber, "Invalid node id in *ELEMENT; skipped.");
                        goto nextRecord;
                    }
                }

                if (model.Elements.Any(e => e.Id == eid))
                {
                    Warn(s.LineNumber, $"Duplicate element id {eid}; skipped.");
                    continue;
                }

                if (!TryCreateElement(eid, elset, typeStr, nodeIds, out var element, out var err))
                {
                    Warn(s.LineNumber, err);
                    continue;
                }

                model.Elements.Add(element);
                nextRecord: ;
            }
        }

        private static bool TryGetNodeCount(string typeUpper, out int count)
        {
            count = typeUpper switch
            {
                "C3D4" => 4,
                "C3D10" => 10,
                "C3D20" or "C3D20R" => 20,
                "S3" => 3,
                "S4" or "S4R" => 4,
                "S6" => 6,
                "S8" or "S8R" => 8,
                _ => 0
            };
            return count > 0;
        }

        private static bool TryCreateElement(int id, string elset, string typeUpper, int[] nodeIds, out IElement element, out string error)
        {
            element = null;
            error = "";
            try
            {
                switch (typeUpper)
                {
                    case "C3D4":
                        element = new Tetra4Element(id, elset, nodeIds);
                        return true;
                    case "C3D10":
                        element = new Tetra10Element(id, elset, nodeIds);
                        return true;
                    case "C3D20":
                    case "C3D20R":
                        element = new Hexa20Element(id, elset, nodeIds);
                        return true;
                    case "S3":
                        element = new Shell3Element(id, elset, nodeIds);
                        return true;
                    case "S4":
                    case "S4R":
                        element = new Shell4Element(id, elset, nodeIds);
                        return true;
                    case "S6":
                        element = new Shell6Element(id, elset, nodeIds);
                        return true;
                    case "S8":
                    case "S8R":
                        element = new Shell8Element(id, elset, nodeIds);
                        return true;
                    default:
                        error = $"Unsupported element type '{typeUpper}'.";
                        return false;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private void ParseElastic(RawSegment s, ref MaterialBase pending)
        {
            if (pending == null)
            {
                Warn(s.LineNumber, "*ELASTIC without preceding *MATERIAL; ignored.");
                return;
            }

            var isEngineering = s.Params.TryGetValue("TYPE", out var t) &&
                                t.Trim().Equals("ENGINEERING CONSTANTS", StringComparison.OrdinalIgnoreCase);

            var records = JoinContinuationRecords(s.DataLines);
            if (records.Count == 0)
                return;

            var line = records[0];
            var fields = SplitNumericFields(line);
            if (isEngineering)
            {
                if (fields.Count < 9)
                {
                    Warn(s.LineNumber, "*ELASTIC,TYPE=ENGINEERING CONSTANTS expects 9 values on first line.");
                    return;
                }

                var ortho = new OrthotropicMaterial(pending.Name) { Density = pending.Density, Color = pending.Color };
                ortho.E1 = ParseDouble(fields[0]);
                ortho.E2 = ParseDouble(fields[1]);
                ortho.E3 = ParseDouble(fields[2]);
                ortho.Nu12 = ParseDouble(fields[3]);
                ortho.Nu13 = ParseDouble(fields[4]);
                ortho.Nu23 = ParseDouble(fields[5]);
                ortho.G12 = ParseDouble(fields[6]);
                ortho.G13 = ParseDouble(fields[7]);
                ortho.G23 = ParseDouble(fields[8]);
                pending = ortho;
            }
            else
            {
                if (pending is not IsotropicMaterial iso)
                {
                    Warn(s.LineNumber, "Isotropic *ELASTIC data ignored (current material is not isotropic).");
                    return;
                }

                if (fields.Count < 2)
                {
                    Warn(s.LineNumber, "*ELASTIC expects Young's modulus and Poisson's ratio.");
                    return;
                }

                iso.YoungModulus = ParseDouble(fields[0]);
                iso.PoissonRatio = ParseDouble(fields[1]);
            }
        }

        private static double ParseDouble(string s) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;

        private void ParseDensity(RawSegment s, ref MaterialBase pending)
        {
            if (pending == null)
            {
                Warn(s.LineNumber, "*DENSITY without preceding *MATERIAL; ignored.");
                return;
            }

            var records = JoinContinuationRecords(s.DataLines);
            if (records.Count == 0)
                return;
            var fields = SplitNumericFields(records[0]);
            if (fields.Count < 1)
                return;
            pending.Density = ParseDouble(fields[0]);
        }

        private void ParsePlastic(RawSegment s, ref MaterialBase pending)
        {
            if (pending is not IsotropicMaterial iso)
            {
                Warn(s.LineNumber, "*PLASTIC is only imported for isotropic metals; ignored.");
                return;
            }

            foreach (var record in JoinContinuationRecords(s.DataLines))
            {
                var fields = SplitNumericFields(record);
                if (fields.Count < 2)
                    continue;
                iso.PlasticCurve.Add(new PlasticPoint(ParseDouble(fields[0]), ParseDouble(fields[1])));
            }
        }

        private void ParseOrientation(Dictionary<string, SectionOrientation> orientations, RawSegment s)
        {
            if (!s.Params.TryGetValue("NAME", out var name) || string.IsNullOrWhiteSpace(name))
            {
                Warn(s.LineNumber, "*ORIENTATION without NAME=; skipped.");
                return;
            }

            name = name.Trim();
            var records = JoinContinuationRecords(s.DataLines);
            if (records.Count == 0)
                return;
            var f = SplitNumericFields(records[0]);
            if (f.Count < 6)
            {
                Warn(s.LineNumber, "*ORIENTATION expects 6 direction cosines.");
                return;
            }

            try
            {
                orientations[name] = new SectionOrientation(
                    ParseDouble(f[0]), ParseDouble(f[1]), ParseDouble(f[2]),
                    ParseDouble(f[3]), ParseDouble(f[4]), ParseDouble(f[5]));
            }
            catch (Exception ex)
            {
                Warn(s.LineNumber, $"*ORIENTATION: {ex.Message}");
            }
        }

        private void ParseSolidSection(
            StructuralModel model,
            Dictionary<string, MaterialBase> materialsByName,
            Dictionary<string, SectionOrientation> orientations,
            RawSegment s)
        {
            if (!s.Params.TryGetValue("ELSET", out var elset) || string.IsNullOrWhiteSpace(elset))
            {
                Warn(s.LineNumber, "*SOLID SECTION without ELSET=; skipped.");
                return;
            }

            if (!s.Params.TryGetValue("MATERIAL", out var matName) || string.IsNullOrWhiteSpace(matName))
            {
                Warn(s.LineNumber, "*SOLID SECTION without MATERIAL=; skipped.");
                return;
            }

            elset = elset.Trim();
            matName = matName.Trim();
            if (!materialsByName.TryGetValue(matName, out var mat))
            {
                Warn(s.LineNumber, $"*SOLID SECTION references unknown material '{matName}'.");
                return;
            }

            SectionOrientation ori = null;
            if (s.Params.TryGetValue("ORIENTATION", out var oname) && !string.IsNullOrWhiteSpace(oname))
            {
                oname = oname.Trim();
                if (!orientations.TryGetValue(oname, out ori))
                    Warn(s.LineNumber, $"*SOLID SECTION references unknown ORIENTATION '{oname}'.");
            }

            model.Sections.Add(new SolidSection(elset, mat, ori));
        }

        private void ParseShellSection(StructuralModel model, Dictionary<string, MaterialBase> materialsByName, RawSegment s)
        {
            if (!s.Params.TryGetValue("ELSET", out var elset) || string.IsNullOrWhiteSpace(elset))
            {
                Warn(s.LineNumber, "*SHELL SECTION without ELSET=; skipped.");
                return;
            }

            if (!s.Params.TryGetValue("MATERIAL", out var matName) || string.IsNullOrWhiteSpace(matName))
            {
                Warn(s.LineNumber, "*SHELL SECTION without MATERIAL=; skipped.");
                return;
            }

            elset = elset.Trim();
            matName = matName.Trim();
            if (!materialsByName.TryGetValue(matName, out var mat))
            {
                Warn(s.LineNumber, $"*SHELL SECTION references unknown material '{matName}'.");
                return;
            }

            var records = JoinContinuationRecords(s.DataLines);
            var t = 1.0;
            if (records.Count > 0)
            {
                var f = SplitNumericFields(records[0]);
                if (f.Count > 0)
                    t = ParseDouble(f[0]);
            }

            if (t <= 0)
            {
                Warn(s.LineNumber, "Shell thickness must be positive; using 1.0.");
                t = 1.0;
            }

            try
            {
                model.Sections.Add(new ShellSection(elset, mat, t));
            }
            catch (Exception ex)
            {
                Warn(s.LineNumber, $"*SHELL SECTION: {ex.Message}");
            }
        }

        private void ParseBoundary(StructuralModel model, Dictionary<string, List<int>> nsets, RawSegment s)
        {
            foreach (var record in JoinContinuationRecords(s.DataLines))
            {
                var fields = SplitNumericFields(record);
                if (fields.Count < 2)
                    continue;

                var a = fields[0].Trim();
                if (!int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var dof1))
                    continue;

                var dof2 = dof1;
                if (fields.Count >= 3 && int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var d2))
                    dof2 = d2;

                if (fields.Count >= 4)
                {
                    Warn(s.LineNumber, "Non-homogeneous *BOUNDARY (prescribed value) is not imported; line skipped.");
                    continue;
                }

                List<int> nodeIds;
                if (int.TryParse(a, NumberStyles.Integer, CultureInfo.InvariantCulture, out var nid))
                {
                    if (model.Nodes.All(n => n.Id != nid))
                    {
                        Warn(s.LineNumber, $"*BOUNDARY references unknown node {nid}; skipped.");
                        continue;
                    }

                    nodeIds = new List<int> { nid };
                }
                else if (nsets.TryGetValue(a, out var set))
                {
                    nodeIds = set.Distinct().ToList();
                    if (nodeIds.Count == 0)
                        continue;
                }
                else
                {
                    Warn(s.LineNumber, $"*BOUNDARY references unknown node or NSET '{a}'; skipped.");
                    continue;
                }

                var (fx, fy, fz, frx, fry, frz) = DofRangeToFlags(dof1, dof2);
                var supportName = $"ImportedBC_{model.FixedSupports.Count + 1}";
                try
                {
                    model.FixedSupports.Add(new FixedSupport(supportName, nodeIds, fx, fy, fz, frx, fry, frz));
                }
                catch (Exception ex)
                {
                    Warn(s.LineNumber, ex.Message);
                }
            }
        }

        private static (bool fx, bool fy, bool fz, bool frx, bool fry, bool frz) DofRangeToFlags(int from, int to)
        {
            var fx = false;
            var fy = false;
            var fz = false;
            var frx = false;
            var fry = false;
            var frz = false;
            var lo = Math.Min(from, to);
            var hi = Math.Max(from, to);
            for (var d = lo; d <= hi; d++)
            {
                switch (d)
                {
                    case 1: fx = true; break;
                    case 2: fy = true; break;
                    case 3: fz = true; break;
                    case 4: frx = true; break;
                    case 5: fry = true; break;
                    case 6: frz = true; break;
                }
            }

            return (fx, fy, fz, frx, fry, frz);
        }

        private bool TryExtractStepBody(
            List<RawSegment> all,
            ref int index,
            RawSegment stepHeader,
            out List<RawSegment> body,
            out bool nlgeom)
        {
            body = new List<RawSegment>();
            nlgeom = stepHeader.Params.ContainsKey("NLGEOM");
            var i = index + 1;
            while (i < all.Count)
            {
                var seg = all[i];
                if (seg.Keyword == "END STEP")
                {
                    index = i;
                    return true;
                }

                body.Add(seg);
                i++;
            }

            Warn(stepHeader.LineNumber, "*STEP without *END STEP; step ignored.");
            body.Clear();
            index = all.Count;
            return false;
        }

        private AnalysisStepBase BuildStep(int stepIndex, List<RawSegment> body, bool nlgeom, int headerLine)
        {
            AnalysisStepBase step = null;
            var propagate = true;

            foreach (var seg in body)
            {
                switch (seg.Keyword)
                {
                    case "STATIC":
                        if (step is FrequencyStep || step is DynamicImplicitStep)
                        {
                            Warn(seg.LineNumber, "*STATIC conflicts with an earlier step procedure; ignored.");
                            break;
                        }

                        if (seg.DataLines.Count > 0 &&
                            !string.IsNullOrWhiteSpace(JoinContinuationRecords(seg.DataLines).FirstOrDefault()))
                        {
                            if (nlgeom)
                            {
                                var nl = new NonlinearStaticStep($"Step-{stepIndex}");
                                var f = SplitNumericFields(JoinContinuationRecords(seg.DataLines)[0]);
                                if (f.Count >= 4)
                                {
                                    nl.InitialIncrement = ParseDouble(f[0]);
                                    nl.TimePeriod = ParseDouble(f[1]);
                                    nl.MinimumIncrement = ParseDouble(f[2]);
                                    nl.MaximumIncrement = ParseDouble(f[3]);
                                }

                                step = nl;
                            }
                            else
                                Warn(seg.LineNumber, "*STATIC with increment line in linear step; using default linear static.");
                        }

                        if (step == null)
                            step = nlgeom
                                ? new NonlinearStaticStep($"Step-{stepIndex}")
                                : new LinearStaticStep($"Step-{stepIndex}");
                        break;

                    case "FREQUENCY":
                        if (step != null)
                        {
                            Warn(seg.LineNumber, "*FREQUENCY conflicts with an earlier step procedure; ignored.");
                            break;
                        }

                        var freq = new FrequencyStep($"Step-{stepIndex}");
                        var fr = JoinContinuationRecords(seg.DataLines).FirstOrDefault();
                        if (!string.IsNullOrEmpty(fr))
                        {
                            var f = SplitNumericFields(fr);
                            if (f.Count >= 1 && int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var nm))
                                freq.NumberOfModes = nm;
                        }

                        step = freq;
                        break;

                    case "DYNAMIC":
                        if (step != null)
                        {
                            Warn(seg.LineNumber, "*DYNAMIC conflicts with an earlier step procedure; ignored.");
                            break;
                        }

                        var dyn = new DynamicImplicitStep($"Step-{stepIndex}");
                        var dr = JoinContinuationRecords(seg.DataLines).FirstOrDefault();
                        if (!string.IsNullOrEmpty(dr))
                        {
                            var f = SplitNumericFields(dr);
                            if (f.Count >= 4)
                            {
                                dyn.InitialIncrement = ParseDouble(f[0]);
                                dyn.TimePeriod = ParseDouble(f[1]);
                                dyn.MinimumIncrement = ParseDouble(f[2]);
                                dyn.MaximumIncrement = ParseDouble(f[3]);
                            }
                        }

                        step = dyn;
                        break;

                    case "CLOAD":
                        if (step == null)
                            step = new LinearStaticStep($"Step-{stepIndex}");
                        if (seg.Params.ContainsKey("OP") && seg.Params["OP"].Equals("NEW", StringComparison.OrdinalIgnoreCase))
                            propagate = false;
                        ParseCload(step, seg);
                        break;

                    case "DLOAD":
                        if (step == null)
                            step = new LinearStaticStep($"Step-{stepIndex}");
                        if (seg.Params.ContainsKey("OP") && seg.Params["OP"].Equals("NEW", StringComparison.OrdinalIgnoreCase))
                            propagate = false;
                        ParseDload(step, seg);
                        break;

                    case "NODE FILE":
                    case "NODE PRINT":
                    case "EL FILE":
                    case "EL PRINT":
                        if (step == null)
                            step = new LinearStaticStep($"Step-{stepIndex}");
                        ParseStepOutput(step, seg);
                        break;

                    default:
                        Warn(seg.LineNumber, $"Unsupported keyword *{seg.Keyword} inside *STEP; skipped.");
                        break;
                }
            }

            if (step == null)
            {
                Warn(headerLine, "*STEP contains no *STATIC, *FREQUENCY, or *DYNAMIC; step skipped.");
                return null;
            }

            step.PropagateLoads = propagate;
            return step;
        }

        private void ParseCload(AnalysisStepBase step, RawSegment s)
        {
            foreach (var record in JoinContinuationRecords(s.DataLines))
            {
                var f = SplitNumericFields(record);
                if (f.Count < 3)
                    continue;
                if (!int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var nid) ||
                    !int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var dofInt) ||
                    !Enum.IsDefined(typeof(StructuralDof), dofInt))
                    continue;
                var dof = (StructuralDof)dofInt;
                var val = ParseDouble(f[2]);
                step.NodalLoads.Add(new NodalLoad(nid, dof, val));
            }
        }

        private void ParseDload(AnalysisStepBase step, RawSegment s)
        {
            foreach (var record in JoinContinuationRecords(s.DataLines))
            {
                var f = SplitNumericFields(record);
                if (f.Count < 6)
                    continue;
                if (!string.Equals(f[1].Trim(), "GRAV", StringComparison.OrdinalIgnoreCase))
                {
                    Warn(s.LineNumber, "*DLOAD type other than GRAV is not imported; skipped.");
                    continue;
                }

                var elset = f[0].Trim();
                var mag = ParseDouble(f[2]);
                var dx = ParseDouble(f[3]);
                var dy = ParseDouble(f[4]);
                var dz = ParseDouble(f[5]);
                try
                {
                    step.GravityLoad = new GravityLoad(mag, dx, dy, dz, elset);
                }
                catch (Exception ex)
                {
                    Warn(s.LineNumber, ex.Message);
                }
            }
        }

        private void ParseStepOutput(AnalysisStepBase step, RawSegment s)
        {
            var vars = new List<string>();
            foreach (var record in JoinContinuationRecords(s.DataLines))
            {
                foreach (var part in record.Split(','))
                {
                    var v = part.Trim();
                    if (v.Length > 0)
                        vars.Add(v);
                }
            }

            if (vars.Count == 0)
                return;

            StepOutputRequest req;
            try
            {
                switch (s.Keyword)
                {
                    case "NODE FILE":
                        req = StepOutputRequest.NodeFileRaw(vars.ToArray());
                        break;
                    case "EL FILE":
                        req = StepOutputRequest.ElementFileRaw(vars.ToArray());
                        break;
                    case "NODE PRINT":
                        s.Params.TryGetValue("NSET", out var ns);
                        req = string.IsNullOrWhiteSpace(ns)
                            ? StepOutputRequest.NodePrintRaw(vars.ToArray())
                            : StepOutputRequest.NodePrintRaw(ns.Trim(), vars.ToArray());
                        break;
                    case "EL PRINT":
                        s.Params.TryGetValue("ELSET", out var es);
                        req = string.IsNullOrWhiteSpace(es)
                            ? StepOutputRequest.ElementPrintRaw(vars.ToArray())
                            : StepOutputRequest.ElementPrintRaw(es.Trim(), vars.ToArray());
                        break;
                    default:
                        return;
                }

                step.OutputRequests.Add(req);
            }
            catch (Exception ex)
            {
                Warn(s.LineNumber, $"Step output: {ex.Message}");
            }
        }

        private static List<string> JoinContinuationRecords(List<string> dataLines)
        {
            var records = new List<string>();
            var sb = new StringBuilder();
            foreach (var raw in dataLines)
            {
                var t = raw.TrimEnd();
                if (t.Length == 0)
                    continue;
                var continued = t.EndsWith(",", StringComparison.Ordinal);
                if (continued)
                    t = t.Substring(0, t.Length - 1).TrimEnd();

                if (sb.Length > 0)
                    sb.Append(',');
                sb.Append(t);
                if (!continued)
                {
                    records.Add(sb.ToString());
                    sb.Clear();
                }
            }

            if (sb.Length > 0)
                records.Add(sb.ToString());

            return records;
        }

        private static List<string> SplitNumericFields(string record)
        {
            return record
                .Split(',')
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();
        }
    }
}
