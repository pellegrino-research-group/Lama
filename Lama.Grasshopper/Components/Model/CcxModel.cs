using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Lama.Core.Materials;
using Lama.Core.Model;
using Lama.Core.Model.Boundary;
using Lama.Core.Model.Elements;
using Lama.Core.Model.Sections;
using Lama.Core.Model.Steps;
using Lama.Gh.Conversion;
using Lama.Gh.Definitions;

namespace Lama.Gh.Components
{
    public class CcxModel : GH_Component
    {
        public CcxModel()
            : base("CcxModel", "CcxModel", "Assemble a CcxModel from element models, supports, and steps.", "Lama", "Model")
        {
            Message = Name + "\nLama";
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "Name", "Model name.", GH_ParamAccess.item, "LamaModel");
            pManager.AddGenericParameter("Element Inputs", "EI", "List of element inputs (StructuralModel fragments, HexMesh definitions, and/or TetraMesh definitions).", GH_ParamAccess.list);
            pManager[1].Optional = true;
            pManager.AddGenericParameter("Supports", "Sup", "FixedSupport list.", GH_ParamAccess.list);
            pManager[2].Optional = true;
            pManager.AddGenericParameter("Steps", "Step", "AnalysisStepBase list.", GH_ParamAccess.list);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("Tolerance", "Tol", "Node merge tolerance across element models.", GH_ParamAccess.item, 1e-6);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Model", "M", "StructuralModel.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var name = "Ccx_Lama_Model";
            var modelObjects = new List<object>();
            var supportObjects = new List<object>();
            var stepObjects = new List<object>();
            var tol = 1e-6;

            DA.GetData(0, ref name);
            DA.GetDataList(1, modelObjects);
            DA.GetDataList(2, supportObjects);
            DA.GetDataList(3, stepObjects);
            DA.GetData(4, ref tol);

            var model = new StructuralModel { Name = name };

            var inferredSections = new List<SectionBase>();
            var sourceModels = BuildSourceModels(modelObjects, inferredSections, tol);
            MergeSourceModels(model, sourceModels, tol);

            var sections = MergeSections(inferredSections);
            foreach (var section in sections)
                model.Sections.Add(section);

            foreach (var support in ExtractInputs<FixedSupport>(supportObjects, "support"))
            {
                var resolvedSupport = ResolveSupportTargets(support, model, tol);
                if (resolvedSupport != null)
                    model.FixedSupports.Add(resolvedSupport);
            }

            foreach (var step in ExtractInputs<AnalysisStepBase>(stepObjects, "step"))
                model.Steps.Add(ResolveNodalLoadTargets(step, model, tol));

            AddDistinctMaterials(model, sections);

            DA.SetData(0, model);
        }

        private AnalysisStepBase ResolveNodalLoadTargets(AnalysisStepBase step, StructuralModel model, double tolerance)
        {
            var resolvedLoads = new List<Lama.Core.Model.Loads.NodalLoad>(step.NodalLoads.Count);
            foreach (var load in step.NodalLoads)
            {
                if (load.HasNodeId)
                {
                    resolvedLoads.Add(load);
                    continue;
                }

                if (!load.X.HasValue || !load.Y.HasValue || !load.Z.HasValue)
                {
                    AddRuntimeMessage(
                        GH_RuntimeMessageLevel.Error,
                        $"Step '{step.Name}' contains an unresolved nodal load without a point target.");
                    continue;
                }

                var nodeId = TryFindNodeId(model, load.X.Value, load.Y.Value, load.Z.Value, tolerance);
                if (!nodeId.HasValue)
                {
                    AddRuntimeMessage(
                        GH_RuntimeMessageLevel.Error,
                        $"No model node found near point ({load.X.Value}, {load.Y.Value}, {load.Z.Value}) for step '{step.Name}'.");
                    continue;
                }

                resolvedLoads.Add(load.ResolveNodeId(nodeId.Value));
            }

            step.NodalLoads.Clear();
            foreach (var load in resolvedLoads)
                step.NodalLoads.Add(load);

            return step;
        }

        private FixedSupport ResolveSupportTargets(FixedSupport support, StructuralModel model, double tolerance)
        {
            if (support.HasNodeIds)
                return support;

            var resolvedNodeIds = new List<int>(support.TargetPoints.Count);
            foreach (var point in support.TargetPoints)
            {
                var nodeId = TryFindNodeId(model, point.X, point.Y, point.Z, tolerance);
                if (!nodeId.HasValue)
                {
                    AddRuntimeMessage(
                        GH_RuntimeMessageLevel.Error,
                        $"No model node found near support point ({point.X}, {point.Y}, {point.Z}) for support '{support.Name}'.");
                    continue;
                }

                resolvedNodeIds.Add(nodeId.Value);
            }

            if (resolvedNodeIds.Count == 0)
                return null;

            return support.ResolveNodeIds(resolvedNodeIds.Distinct());
        }

        private List<StructuralModel> BuildSourceModels(IEnumerable<object> modelObjects, IList<SectionBase> inferredSections, double tolerance)
        {
            var sources = new List<StructuralModel>();
            foreach (var obj in modelObjects)
            {
                var input = UnwrapElementInput(obj);

                if (input is StructuralModel sourceModel)
                {
                    sources.Add(sourceModel);
                    foreach (var section in sourceModel.Sections.OfType<SectionBase>())
                        inferredSections.Add(section);
                    continue;
                }

                if (input is HexMeshDefinition hexMesh)
                {
                    var converted = RhinoHexMeshToLamaConverter.CreateModelFromHexMeshes(
                        hexMesh.Meshes,
                        tolerance,
                        modelName: "HexMeshFragment",
                        elementSetName: hexMesh.ElementSetName);
                    sources.Add(converted);

                    if (hexMesh.Material != null)
                        inferredSections.Add(new SolidSection(hexMesh.ElementSetName, hexMesh.Material, hexMesh.Orientation));

                    continue;
                }

                if (input is TetraMeshDefinition tetraMesh)
                {
                    var converted = RhinoTetraMeshToLamaConverter.CreateModelFromTetraMeshes(
                        tetraMesh.Meshes,
                        tolerance,
                        modelName: "TetraMeshFragment",
                        elementSetName: tetraMesh.ElementSetName);
                    sources.Add(converted);

                    if (tetraMesh.Material != null)
                        inferredSections.Add(new SolidSection(tetraMesh.ElementSetName, tetraMesh.Material, tetraMesh.Orientation));

                    continue;
                }

                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Warning,
                    $"Unsupported element input type '{input?.GetType().Name ?? obj?.GetType().Name ?? "null"}'.");
            }

            return sources;
        }

        private static object UnwrapElementInput(object input)
        {
            if (input == null)
                return null;

            if (input is IGH_Goo goo)
            {
                var scriptValue = goo.ScriptVariable();
                if (scriptValue != null && !ReferenceEquals(scriptValue, input))
                    return scriptValue;
            }

            var valueProp = input.GetType().GetProperty("Value");
            if (valueProp != null && valueProp.GetIndexParameters().Length == 0)
            {
                try
                {
                    var value = valueProp.GetValue(input);
                    if (value != null && !ReferenceEquals(value, input))
                        return value;
                }
                catch
                {
                    // ignored
                }
            }

            return input;
        }

        private IEnumerable<T> ExtractInputs<T>(IEnumerable<object> rawInputs, string inputKind) where T : class
        {
            foreach (var raw in rawInputs)
            {
                var input = UnwrapElementInput(raw);
                if (input is T typed)
                {
                    yield return typed;
                    continue;
                }

                if (raw == null)
                    continue;

                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Warning,
                    $"Unsupported {inputKind} input type '{input?.GetType().Name ?? raw.GetType().Name}'.");
            }
        }

        private List<SectionBase> MergeSections(IEnumerable<SectionBase> inferredSections)
        {
            // Key by ELSET so section sources remain unique after combining model fragments.
            var merged = new Dictionary<string, SectionBase>(StringComparer.OrdinalIgnoreCase);

            foreach (var section in inferredSections.Where(s => s != null))
                merged[section.ElementSetName] = section;

            return merged.Values.ToList();
        }

        private static void MergeSourceModels(StructuralModel target, IReadOnlyList<StructuralModel> sources, double tolerance)
        {
            var nodeMap = new Dictionary<NodeKey, int>();
            var nextNodeId = 1;
            var nextElementId = 1;

            foreach (var source in sources)
            {
                var localNodeIdMap = new Dictionary<int, int>();
                foreach (var node in source.Nodes)
                {
                    var key = NodeKey.From(node.X, node.Y, node.Z, tolerance);
                    if (!nodeMap.TryGetValue(key, out var globalNodeId))
                    {
                        globalNodeId = nextNodeId++;
                        nodeMap[key] = globalNodeId;
                        target.Nodes.Add(new Node(globalNodeId, node.X, node.Y, node.Z));
                    }

                    localNodeIdMap[node.Id] = globalNodeId;
                }

                foreach (var element in source.Elements)
                {
                    var remapped = element.NodeIds.Select(id => localNodeIdMap[id]).ToArray();
                    target.Elements.Add(CloneElement(nextElementId++, element.ElementSetName, element.ElementType, remapped));
                }
            }
        }

        private static int? TryFindNodeId(StructuralModel model, double x, double y, double z, double tolerance)
        {
            if (tolerance <= 0)
                tolerance = 1e-9;

            var tolSq = tolerance * tolerance;
            foreach (var node in model.Nodes)
            {
                var dx = node.X - x;
                var dy = node.Y - y;
                var dz = node.Z - z;
                var distSq = (dx * dx) + (dy * dy) + (dz * dz);
                if (distSq <= tolSq)
                    return node.Id;
            }

            return null;
        }

        private static IElement CloneElement(int id, string elementSetName, CalculixElementType type, IReadOnlyList<int> nodeIds)
        {
            switch (type)
            {
                case CalculixElementType.C3D4:
                    return new Tetra4Element(id, elementSetName, nodeIds);
                case CalculixElementType.C3D10:
                    return new Tetra10Element(id, elementSetName, nodeIds);
                case CalculixElementType.C3D20R:
                    return new Hexa20Element(id, elementSetName, nodeIds);
                case CalculixElementType.S3:
                    return new Shell3Element(id, elementSetName, nodeIds);
                case CalculixElementType.S4:
                    return new Shell4Element(id, elementSetName, nodeIds);
                case CalculixElementType.S6:
                    return new Shell6Element(id, elementSetName, nodeIds);
                case CalculixElementType.S8R:
                    return new Shell8Element(id, elementSetName, nodeIds);
                default:
                    throw new NotSupportedException($"Unsupported element type '{type}'.");
            }
        }

        private static void AddDistinctMaterials(StructuralModel model, IEnumerable<SectionBase> sections)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var material in model.Materials.OfType<MaterialBase>())
                seen.Add(material.Name);

            foreach (var section in sections)
            {
                if (section?.Material == null)
                    continue;

                if (!seen.Add(section.Material.Name))
                    continue;

                model.Materials.Add(section.Material);
            }
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

            public static NodeKey From(double x, double y, double z, double tolerance)
            {
                if (tolerance <= 0)
                    tolerance = 1e-9;

                var inv = 1.0 / tolerance;
                return new NodeKey(
                    (long)Math.Round(x * inv, MidpointRounding.AwayFromZero),
                    (long)Math.Round(y * inv, MidpointRounding.AwayFromZero),
                    (long)Math.Round(z * inv, MidpointRounding.AwayFromZero));
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

        protected override System.Drawing.Bitmap Icon => Lama.Gh.Properties.Resources.Lama_24x24;
        public override Guid ComponentGuid => new Guid("1158a4ed-f262-4601-8e4f-349e72853a13");
    }
}
