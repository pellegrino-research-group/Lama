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
    /// Builds CalculiX input deck text from a structural model.
    /// </summary>
    public sealed class CalculixInputDeckBuilder
    {
        private readonly Dictionary<FixedSupport, string> _supportSetNameMap = new Dictionary<FixedSupport, string>();

        public string Build(StructuralModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            model.Validate();

            var builder = new StringBuilder();

            WriteHeading(builder, model);
            WriteNodes(builder, model);
            WriteElements(builder, model);
            WriteSupportsAsNodeSets(builder, model);
            WriteMaterials(builder, model);
            WriteSections(builder, model);
            WriteBoundaryConditions(builder, model);
            WriteSteps(builder, model);

            return builder.ToString();
        }

        public void WriteToFile(StructuralModel model, string inputFilePath)
        {
            if (string.IsNullOrWhiteSpace(inputFilePath))
                throw new ArgumentException("Input file path cannot be empty.", nameof(inputFilePath));

            var content = Build(model);
            File.WriteAllText(inputFilePath, content);
        }

        private static void WriteHeading(StringBuilder builder, StructuralModel model)
        {
            builder.AppendLine(CalculixKeywords.Heading);
            builder.AppendLine(model.Name);
        }

        private static void WriteNodes(StringBuilder builder, StructuralModel model)
        {
            builder.AppendLine(CalculixKeywords.Node);
            foreach (var node in model.Nodes.OrderBy(n => n.Id))
            {
                builder.AppendLine(FormattableString.Invariant(
                    $"{node.Id},{node.X},{node.Y},{node.Z}"));
            }
        }

        private static void WriteElements(StringBuilder builder, StructuralModel model)
        {
            var groups = model.Elements
                .GroupBy(e => new
                {
                    ElementType = e.ElementType,
                    ElementSetName = e.ElementSetName.ToUpperInvariant()
                })
                .OrderBy(g => g.Key.ElementType.ToString(), StringComparer.Ordinal)
                .ThenBy(g => g.Key.ElementSetName, StringComparer.Ordinal);

            foreach (var group in groups)
            {
                builder.AppendLine(FormattableString.Invariant(
                    $"{CalculixKeywords.Element},TYPE={group.Key.ElementType},ELSET={group.Key.ElementSetName}"));

                foreach (var element in group.OrderBy(e => e.Id))
                    WriteElementConnectivity(builder, element.Id, element.NodeIds);
            }
        }

        private static void WriteElementConnectivity(StringBuilder builder, int elementId, IReadOnlyList<int> nodeIds)
        {
            var entries = new List<string>(1 + nodeIds.Count)
            {
                elementId.ToString(CultureInfo.InvariantCulture)
            };
            entries.AddRange(nodeIds.Select(id => id.ToString(CultureInfo.InvariantCulture)));

            const int maxEntriesPerLine = 16;
            for (var i = 0; i < entries.Count; i += maxEntriesPerLine)
            {
                var count = Math.Min(maxEntriesPerLine, entries.Count - i);
                var line = string.Join(",", entries.Skip(i).Take(count));

                // Continuation lines for long connectivities require a trailing comma.
                if (i + count < entries.Count)
                    line += ",";

                builder.AppendLine(line);
            }
        }

        private void WriteSupportsAsNodeSets(StringBuilder builder, StructuralModel model)
        {
            for (var i = 0; i < model.FixedSupports.Count; i++)
            {
                var support = model.FixedSupports[i];
                var setName = $"SUPPORT_{i + 1}_{SanitizeName(support.Name)}";
                _supportSetNameMap[support] = setName;

                builder.AppendLine(FormattableString.Invariant($"{CalculixKeywords.Nset},NSET={setName}"));
                WriteIntegerList(builder, support.NodeIds.OrderBy(n => n));
            }
        }

        private static void WriteIntegerList(StringBuilder builder, IEnumerable<int> values, int perLine = 16)
        {
            var buffer = new List<int>(perLine);
            foreach (var value in values)
            {
                buffer.Add(value);
                if (buffer.Count < perLine)
                    continue;

                builder.AppendLine(string.Join(",", buffer) + ",");
                buffer.Clear();
            }

            if (buffer.Count > 0)
                builder.AppendLine(string.Join(",", buffer) + ",");
        }

        private static void WriteMaterials(StringBuilder builder, StructuralModel model)
        {
            foreach (var material in model.Materials.OfType<IsotropicMaterial>())
            {
                builder.AppendLine(FormattableString.Invariant($"{CalculixKeywords.Material},NAME={material.Name}"));
                builder.AppendLine(CalculixKeywords.Elastic);
                builder.AppendLine(FormattableString.Invariant($"{material.YoungModulus},{material.PoissonRatio}"));

                if (!material.HasPlasticity)
                    continue;

                builder.AppendLine(CalculixKeywords.Plastic);
                foreach (var point in material.PlasticCurve)
                {
                    builder.AppendLine(FormattableString.Invariant(
                        $"{point.YieldStress},{point.EquivalentPlasticStrain}"));
                }
            }
        }

        private static void WriteSections(StringBuilder builder, StructuralModel model)
        {
            foreach (var section in model.Sections)
            {
                switch (section)
                {
                    case SolidSection solid:
                        WriteSolidSection(builder, solid);
                        break;
                    case ShellSection shell:
                        WriteShellSection(builder, shell);
                        break;
                    case BeamSection:
                        throw new NotSupportedException("Beam section export is not implemented yet.");
                    default:
                        throw new NotSupportedException($"Unsupported section type '{section.GetType().Name}'.");
                }
            }
        }

        private static void WriteSolidSection(StringBuilder builder, SolidSection section)
        {
            builder.AppendLine(FormattableString.Invariant(
                $"{CalculixKeywords.SolidSection},ELSET={section.ElementSetName},MATERIAL={section.Material.Name}"));
        }

        private static void WriteShellSection(StringBuilder builder, ShellSection section)
        {
            builder.AppendLine(FormattableString.Invariant(
                $"{CalculixKeywords.ShellSection},ELSET={section.ElementSetName},MATERIAL={section.Material.Name}"));

            // Keep export robust: per-element/per-node shell thickness is stored in the model
            // and can be promoted to dedicated deck cards in a follow-up.
            builder.AppendLine(FormattableString.Invariant($"{section.UniformThickness}"));
        }

        private void WriteBoundaryConditions(StringBuilder builder, StructuralModel model)
        {
            if (model.FixedSupports.Count == 0)
                return;

            builder.AppendLine(CalculixKeywords.Boundary);
            foreach (var support in model.FixedSupports)
            {
                var setName = _supportSetNameMap[support];
                if (support.FixTranslations)
                    builder.AppendLine($"{setName},1,3");
                if (support.FixRotations)
                    builder.AppendLine($"{setName},4,6");
            }
        }

        private static void WriteSteps(StringBuilder builder, StructuralModel model)
        {
            foreach (var step in model.Steps)
            {
                WriteStep(builder, step);
            }
        }

        private static void WriteStep(StringBuilder builder, AnalysisStepBase step)
        {
            var stepHeader = step is NonlinearStaticStep ? $"{CalculixKeywords.Step},NLGEOM" : CalculixKeywords.Step;
            builder.AppendLine(stepHeader);
            WriteProcedure(builder, step);
            WriteStepLoads(builder, step.NodalLoads);
            WriteStepOutputs(builder, step.OutputRequests);
            builder.AppendLine(CalculixKeywords.EndStep);
        }

        private static void WriteProcedure(StringBuilder builder, AnalysisStepBase step)
        {
            switch (step)
            {
                case LinearStaticStep:
                    builder.AppendLine(CalculixKeywords.Static);
                    break;
                case NonlinearStaticStep nonlinear:
                    builder.AppendLine(CalculixKeywords.Static);
                    builder.AppendLine(FormattableString.Invariant(
                        $"{nonlinear.InitialIncrement},{nonlinear.TimePeriod},{nonlinear.MinimumIncrement},{nonlinear.MaximumIncrement}"));
                    break;
                case FrequencyStep frequency:
                    builder.AppendLine(CalculixKeywords.Frequency);
                    builder.AppendLine(frequency.NumberOfModes.ToString(CultureInfo.InvariantCulture));
                    break;
                case DynamicImplicitStep dynamic:
                    builder.AppendLine(CalculixKeywords.Dynamic);
                    builder.AppendLine(FormattableString.Invariant(
                        $"{dynamic.InitialIncrement},{dynamic.TimePeriod},{dynamic.MinimumIncrement},{dynamic.MaximumIncrement}"));
                    break;
                default:
                    throw new NotSupportedException($"Unsupported step type '{step.GetType().Name}'.");
            }
        }

        private static void WriteStepLoads(StringBuilder builder, IEnumerable<NodalLoad> loads)
        {
            var loadList = loads.ToList();
            if (loadList.Count == 0)
                return;

            builder.AppendLine(CalculixKeywords.Cload);
            foreach (var load in loadList)
            {
                builder.AppendLine(FormattableString.Invariant($"{load.NodeId},{(int)load.Dof},{load.Value}"));
            }
        }

        private static void WriteStepOutputs(StringBuilder builder, IEnumerable<StepOutputRequest> outputRequests)
        {
            var requests = outputRequests.ToList();
            if (requests.Count == 0)
                return;

            foreach (var request in requests)
            {
                builder.AppendLine(GetOutputKeyword(request));
                builder.AppendLine(string.Join(",", request.Variables));
            }
        }

        private static string GetOutputKeyword(StepOutputRequest request)
        {
            switch (request.OutputType)
            {
                case StepOutputType.NodeFile:
                    return CalculixKeywords.NodeFile;
                case StepOutputType.ElementFile:
                    return CalculixKeywords.ElementFile;
                case StepOutputType.NodePrint:
                    return string.IsNullOrWhiteSpace(request.TargetSetName)
                        ? CalculixKeywords.NodePrint
                        : $"{CalculixKeywords.NodePrint},NSET={request.TargetSetName}";
                case StepOutputType.ElementPrint:
                    return string.IsNullOrWhiteSpace(request.TargetSetName)
                        ? CalculixKeywords.ElementPrint
                        : $"{CalculixKeywords.ElementPrint},ELSET={request.TargetSetName}";
                default:
                    throw new NotSupportedException($"Unsupported step output type '{request.OutputType}'.");
            }
        }

        private static string SanitizeName(string value)
        {
            var chars = value
                .Trim()
                .Select(c => char.IsLetterOrDigit(c) ? c : '_')
                .ToArray();

            return new string(chars);
        }
    }
}
