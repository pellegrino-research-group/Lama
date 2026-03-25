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

            var needsNall = NeedsAllNodesSet(model);
            var needsEall = NeedsAllElementsSet(model);

            WriteHeading(builder, model);
            WriteNodes(builder, model);
            WriteElements(builder, model);
            if (needsNall)
                WriteAllNodesSet(builder, model);
            if (needsEall)
                WriteAllElementsSet(builder, model);
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
            foreach (var material in model.Materials)
            {
                switch (material)
                {
                    case IsotropicMaterial isotropic:
                        WriteIsotropicMaterial(builder, isotropic);
                        break;
                    case OrthotropicMaterial orthotropic:
                        WriteOrthotropicMaterial(builder, orthotropic);
                        break;
                    case SpringMaterial spring:
                        throw new NotSupportedException(
                            $"Material '{spring.Name}' is SpringMaterial, but spring export cards are not implemented yet.");
                    case StiffnessMatrixMaterial stiffness:
                        throw new NotSupportedException(
                            $"Material '{stiffness.Name}' is StiffnessMatrixMaterial, but stiffness-matrix export cards are not implemented yet.");
                    default:
                        throw new NotSupportedException(
                            $"Unsupported material type '{material.GetType().Name}'.");
                }
            }
        }

        private static void WriteIsotropicMaterial(StringBuilder builder, IsotropicMaterial material)
        {
            builder.AppendLine(FormattableString.Invariant($"{CalculixKeywords.Material},NAME={material.Name}"));
            builder.AppendLine(CalculixKeywords.Elastic);
            builder.AppendLine(FormattableString.Invariant($"{material.YoungModulus},{material.PoissonRatio}"));
            builder.AppendLine(CalculixKeywords.Density);
            builder.AppendLine(FormattableString.Invariant($"{material.Density}"));

            if (!material.HasPlasticity)
                return;

            builder.AppendLine(CalculixKeywords.Plastic);
            foreach (var point in material.PlasticCurve)
            {
                builder.AppendLine(FormattableString.Invariant(
                    $"{point.YieldStress},{point.EquivalentPlasticStrain}"));
            }
        }

        private static void WriteOrthotropicMaterial(StringBuilder builder, OrthotropicMaterial material)
        {
            builder.AppendLine(FormattableString.Invariant($"{CalculixKeywords.Material},NAME={material.Name}"));
            builder.AppendLine(CalculixKeywords.ElasticEngineering);
            builder.AppendLine(FormattableString.Invariant(
                $"{material.E1},{material.E2},{material.E3},{material.Nu12},{material.Nu13},{material.Nu23},{material.G12},{material.G13},{material.G23}"));
            builder.AppendLine(CalculixKeywords.Density);
            builder.AppendLine(FormattableString.Invariant($"{material.Density}"));
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
            if (section.Orientation != null)
            {
                var orientationName = $"ORI_{SanitizeName(section.ElementSetName)}";
                builder.AppendLine(FormattableString.Invariant(
                    $"{CalculixKeywords.Orientation},NAME={orientationName}"));
                builder.AppendLine(FormattableString.Invariant(
                    $"{section.Orientation.Axis1X},{section.Orientation.Axis1Y},{section.Orientation.Axis1Z},{section.Orientation.Axis2X},{section.Orientation.Axis2Y},{section.Orientation.Axis2Z}"));
                builder.AppendLine(FormattableString.Invariant(
                    $"{CalculixKeywords.SolidSection},ELSET={section.ElementSetName},MATERIAL={section.Material.Name},ORIENTATION={orientationName}"));
                return;
            }

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
                WriteFixedSupportDofs(builder, setName, support);
            }
        }

        private static void WriteFixedSupportDofs(StringBuilder builder, string setName, FixedSupport support)
        {
            int? rangeStart = null;
            for (var d = 1; d <= 6; d++)
            {
                var fix = support.IsDofFixed(d);
                if (fix)
                {
                    if (!rangeStart.HasValue)
                        rangeStart = d;
                }
                else
                {
                    if (rangeStart.HasValue)
                    {
                        var from = rangeStart.Value;
                        builder.AppendLine(FormattableString.Invariant($"{setName},{from},{d - 1}"));
                        rangeStart = null;
                    }
                }
            }

            if (rangeStart.HasValue)
                builder.AppendLine(FormattableString.Invariant($"{setName},{rangeStart.Value},6"));
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
            WriteStepLoads(builder, step.NodalLoads, step.PropagateLoads);
            WriteStepGravityLoad(builder, step.GravityLoad, step.PropagateLoads);
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

        private static void WriteStepLoads(StringBuilder builder, IEnumerable<NodalLoad> loads, bool propagate)
        {
            var loadList = loads.ToList();
            if (loadList.Count == 0)
                return;

            var keyword = propagate ? CalculixKeywords.Cload : $"{CalculixKeywords.Cload},OP=NEW";
            builder.AppendLine(keyword);
            foreach (var load in loadList)
            {
                builder.AppendLine(FormattableString.Invariant($"{load.NodeId},{(int)load.Dof},{load.Value}"));
            }
        }

        private static void WriteStepGravityLoad(StringBuilder builder, GravityLoad g, bool propagate)
        {
            if (g == null)
                return;

            var set = string.IsNullOrWhiteSpace(g.ElementSetName) ? AllElementsSetName : g.ElementSetName;
            var keyword = propagate ? CalculixKeywords.Dload : $"{CalculixKeywords.Dload},OP=NEW";
            builder.AppendLine(keyword);
            builder.AppendLine(FormattableString.Invariant(
                $"{set},GRAV,{g.Magnitude},{g.DirectionX},{g.DirectionY},{g.DirectionZ}"));
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
                    var nset = string.IsNullOrWhiteSpace(request.TargetSetName) ? AllNodesSetName : request.TargetSetName;
                    return $"{CalculixKeywords.NodePrint},NSET={nset}";
                case StepOutputType.ElementPrint:
                    var elset = string.IsNullOrWhiteSpace(request.TargetSetName) ? AllElementsSetName : request.TargetSetName;
                    return $"{CalculixKeywords.ElementPrint},ELSET={elset}";
                default:
                    throw new NotSupportedException($"Unsupported step output type '{request.OutputType}'.");
            }
        }

        private const string AllNodesSetName = "NALL";
        private const string AllElementsSetName = "EALL";

        private static bool NeedsAllNodesSet(StructuralModel model)
        {
            return model.Steps
                .SelectMany(s => s.OutputRequests)
                .Any(r => r.OutputType == StepOutputType.NodePrint && string.IsNullOrWhiteSpace(r.TargetSetName));
        }

        private static bool NeedsAllElementsSet(StructuralModel model)
        {
            var needsForOutput = model.Steps
                .SelectMany(s => s.OutputRequests)
                .Any(r => r.OutputType == StepOutputType.ElementPrint && string.IsNullOrWhiteSpace(r.TargetSetName));

            var needsForGravity = model.Steps
                .Where(s => s.GravityLoad != null)
                .Any(s => string.IsNullOrWhiteSpace(s.GravityLoad.ElementSetName));

            return needsForOutput || needsForGravity;
        }

        private static void WriteAllNodesSet(StringBuilder builder, StructuralModel model)
        {
            builder.AppendLine(FormattableString.Invariant($"{CalculixKeywords.Nset},NSET={AllNodesSetName}"));
            WriteIntegerList(builder, model.Nodes.Select(n => n.Id).OrderBy(id => id));
        }

        private static void WriteAllElementsSet(StringBuilder builder, StructuralModel model)
        {
            builder.AppendLine(FormattableString.Invariant($"{CalculixKeywords.Elset},ELSET={AllElementsSetName}"));
            WriteIntegerList(builder, model.Elements.Select(e => e.Id).OrderBy(id => id));
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
