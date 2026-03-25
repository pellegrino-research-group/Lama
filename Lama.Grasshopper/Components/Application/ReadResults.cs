using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Lama.Core.Model;
using Lama.Core.PostProcessing;
using Lama.Grasshopper.Widgets;
using Rhino.Geometry;

namespace Lama.Grasshopper.Components
{
    public class ReadResultsComponent : GH_SwitcherComponent
    {
        public ReadResultsComponent()
            : base("ReadResults", "ReadDat",
                "Read nodal and element results from a CalculiX .dat file.", "Lama", "Application")
        {
        }

        protected override string DefaultEvaluationUnit => "ReadResults";

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // Parameters are registered via EvaluationUnits.
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            // Parameters are registered via EvaluationUnits.
        }

        protected override void RegisterEvaluationUnits(EvaluationUnitManager mngr)
        {
            var unit = new EvaluationUnit("ReadResults", "ReadResults", "Read nodal and element results from DAT.");

            unit.RegisterInputParam(new Param_String(), "Dat Path", "Dat", "Path to the .dat file.", GH_ParamAccess.item);
            unit.RegisterInputParam(new Param_GenericObject(), "Model", "M", "StructuralModel used to map result IDs to positions.", GH_ParamAccess.item);

            unit.RegisterOutputParam(new Param_String(), "Dat Path", "Path", "Resolved .dat file path.");
            unit.RegisterOutputParam(new Param_Point(), "Node Positions", "P", "Node positions aligned with U.");
            unit.RegisterOutputParam(new Param_Vector(), "Node Displacements", "U", "Nodal displacement vectors.");
            unit.RegisterOutputParam(new Param_Point(), "Stress Positions", "Sp", "Element stress positions aligned with stress columns.");
            unit.RegisterOutputParam(new Param_Number(), "Sxx", "Sxx", "Normal stress xx.");
            unit.RegisterOutputParam(new Param_Number(), "Syy", "Syy", "Normal stress yy.");
            unit.RegisterOutputParam(new Param_Number(), "Szz", "Szz", "Normal stress zz.");
            unit.RegisterOutputParam(new Param_Number(), "Sxy", "Sxy", "Shear stress xy.");
            unit.RegisterOutputParam(new Param_Number(), "Sxz", "Sxz", "Shear stress xz.");
            unit.RegisterOutputParam(new Param_Number(), "Syz", "Syz", "Shear stress yz.");
            unit.RegisterOutputParam(new Param_Number(), "SvM", "SvM", "Von Mises stress.");

            var nodalMenu = new GH_ExtendableMenu(0, "menu_nodal") { Name = "Nodal Results" };
            nodalMenu.RegisterOutputPlug(unit.Outputs[1]); // P
            nodalMenu.RegisterOutputPlug(unit.Outputs[2]); // U
            nodalMenu.Collapse();
            unit.AddMenu(nodalMenu);

            var elementMenu = new GH_ExtendableMenu(1, "menu_element") { Name = "Element Results" };
            elementMenu.RegisterOutputPlug(unit.Outputs[0]);  // Path
            elementMenu.RegisterOutputPlug(unit.Outputs[3]);  // Sp
            elementMenu.RegisterOutputPlug(unit.Outputs[4]);  // Sxx
            elementMenu.RegisterOutputPlug(unit.Outputs[5]);  // Syy
            elementMenu.RegisterOutputPlug(unit.Outputs[6]);  // Szz
            elementMenu.RegisterOutputPlug(unit.Outputs[7]);  // Sxy
            elementMenu.RegisterOutputPlug(unit.Outputs[8]);  // Sxz
            elementMenu.RegisterOutputPlug(unit.Outputs[9]);  // Syz
            elementMenu.RegisterOutputPlug(unit.Outputs[10]); // SvM
            elementMenu.Collapse();
            unit.AddMenu(elementMenu);

            mngr.RegisterUnit(unit);
        }

        protected override void SolveInstance(IGH_DataAccess DA, EvaluationUnit unit)
        {
            var datPath = string.Empty;
            object modelObj = null;
            if (!DA.GetData(0, ref datPath))
                return;
            if (!DA.GetData(1, ref modelObj))
                return;

            DA.SetData(0, datPath);

            var emptyPoints = new Point3d[0];
            var emptyVectors = new Vector3d[0];
            var emptyNumbers = new double[0];

            if (string.IsNullOrWhiteSpace(datPath) || !File.Exists(datPath))
            {
                SetEmptyOutputs(DA, emptyPoints, emptyVectors, emptyNumbers);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    $"DAT file not found at '{datPath}'. Provide a valid .dat path.");
                return;
            }

            if (!TryUnwrapStructuralModel(modelObj, out var model))
            {
                SetEmptyOutputs(DA, emptyPoints, emptyVectors, emptyNumbers);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Model input must be a StructuralModel.");
                return;
            }

            var nodeMap = model.Nodes.ToDictionary(n => n.Id);
            var elementCentroids = BuildElementCentroidMap(model, nodeMap);

            try
            {
                var tables = CalculixDatParser.ParseFile(datPath);

                // Nodal outputs
                if (CalculixDatExtractors.TryGetNodalDisplacements(tables, out var displacements))
                {
                    DA.SetDataList(1, MapNodePoints(displacements, nodeMap));
                    DA.SetDataList(2, MapDisplacementVectors(displacements));
                }
                else
                {
                    DA.SetDataList(1, emptyPoints);
                    DA.SetDataList(2, emptyVectors);
                }

                // Element outputs
                if (CalculixDatExtractors.TryGetElementStress(tables, out var stresses))
                {
                    var stressPoints = new List<Point3d>();
                    var sxx = new List<double>();
                    var syy = new List<double>();
                    var szz = new List<double>();
                    var sxy = new List<double>();
                    var sxz = new List<double>();
                    var syz = new List<double>();
                    var svm = new List<double>();

                    foreach (var stress in stresses)
                    {
                        var c = stress.Components;
                        double sxxVal, syyVal, szzVal, sxyVal, sxzVal, syzVal;

                        if (c.Count >= 7)
                        {
                            // c[0] = integration point index (discard)
                            sxxVal = c[1]; syyVal = c[2]; szzVal = c[3];
                            sxyVal = c[4]; sxzVal = c[5]; syzVal = c[6];
                        }
                        else if (c.Count >= 6)
                        {
                            // Fallback when integration point is already omitted.
                            sxxVal = c[0]; syyVal = c[1]; szzVal = c[2];
                            sxyVal = c[3]; sxzVal = c[4]; syzVal = c[5];
                        }
                        else
                        {
                            continue;
                        }

                        if (!elementCentroids.TryGetValue(stress.ElementId, out var centroid))
                            centroid = Point3d.Unset;

                        stressPoints.Add(centroid);
                        sxx.Add(sxxVal);
                        syy.Add(syyVal);
                        szz.Add(szzVal);
                        sxy.Add(sxyVal);
                        sxz.Add(sxzVal);
                        syz.Add(syzVal);
                        svm.Add(ComputeVonMises(sxxVal, syyVal, szzVal, sxyVal, sxzVal, syzVal));
                    }

                    DA.SetDataList(3, stressPoints);
                    DA.SetDataList(4, sxx);
                    DA.SetDataList(5, syy);
                    DA.SetDataList(6, szz);
                    DA.SetDataList(7, sxy);
                    DA.SetDataList(8, sxz);
                    DA.SetDataList(9, syz);
                    DA.SetDataList(10, svm);
                }
                else
                {
                    DA.SetDataList(3, emptyPoints);
                    DA.SetDataList(4, emptyNumbers);
                    DA.SetDataList(5, emptyNumbers);
                    DA.SetDataList(6, emptyNumbers);
                    DA.SetDataList(7, emptyNumbers);
                    DA.SetDataList(8, emptyNumbers);
                    DA.SetDataList(9, emptyNumbers);
                    DA.SetDataList(10, emptyNumbers);
                }
            }
            catch (Exception ex)
            {
                SetEmptyOutputs(DA, emptyPoints, emptyVectors, emptyNumbers);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Failed to parse DAT: {ex.Message}");
            }
        }

        private static void SetEmptyOutputs(
            IGH_DataAccess DA,
            IReadOnlyList<Point3d> emptyPoints,
            IReadOnlyList<Vector3d> emptyVectors,
            IReadOnlyList<double> emptyNumbers)
        {
            DA.SetDataList(1, emptyPoints);
            DA.SetDataList(2, emptyVectors);
            DA.SetDataList(3, emptyPoints);
            DA.SetDataList(4, emptyNumbers);
            DA.SetDataList(5, emptyNumbers);
            DA.SetDataList(6, emptyNumbers);
            DA.SetDataList(7, emptyNumbers);
            DA.SetDataList(8, emptyNumbers);
            DA.SetDataList(9, emptyNumbers);
            DA.SetDataList(10, emptyNumbers);
        }

        private static Dictionary<int, Point3d> BuildElementCentroidMap(
            StructuralModel model,
            IReadOnlyDictionary<int, Node> nodeMap)
        {
            var centroids = new Dictionary<int, Point3d>();
            foreach (var element in model.Elements)
            {
                double x = 0, y = 0, z = 0;
                var count = 0;
                foreach (var nodeId in element.NodeIds)
                {
                    if (!nodeMap.TryGetValue(nodeId, out var node))
                        continue;
                    x += node.X;
                    y += node.Y;
                    z += node.Z;
                    count++;
                }

                centroids[element.Id] = count > 0
                    ? new Point3d(x / count, y / count, z / count)
                    : Point3d.Unset;
            }

            return centroids;
        }

        private static List<Point3d> MapNodePoints(
            IEnumerable<NodalVectorResult> vectors,
            IReadOnlyDictionary<int, Node> nodeMap)
        {
            var points = new List<Point3d>();
            if (vectors == null || nodeMap == null || nodeMap.Count == 0)
                return points;

            foreach (var vector in vectors)
            {
                if (!nodeMap.TryGetValue(vector.NodeId, out var node))
                {
                    points.Add(Point3d.Unset);
                    continue;
                }

                points.Add(new Point3d(node.X, node.Y, node.Z));
            }

            return points;
        }

        private static List<Vector3d> MapDisplacementVectors(IEnumerable<NodalVectorResult> vectors)
        {
            var displacements = new List<Vector3d>();
            if (vectors == null)
                return displacements;

            foreach (var vector in vectors)
                displacements.Add(new Vector3d(vector.X, vector.Y, vector.Z));

            return displacements;
        }

        private static double ComputeVonMises(double sxx, double syy, double szz, double sxy, double sxz, double syz)
        {
            var normal = 0.5 * (
                (sxx - syy) * (sxx - syy) +
                (syy - szz) * (syy - szz) +
                (szz - sxx) * (szz - sxx));
            var shear = 3.0 * ((sxy * sxy) + (sxz * sxz) + (syz * syz));
            return Math.Sqrt(Math.Max(0.0, normal + shear));
        }

        private static bool TryUnwrapStructuralModel(object input, out StructuralModel model)
        {
            model = input as StructuralModel;
            if (model != null)
                return true;

            if (input is IGH_Goo goo)
            {
                var scriptValue = goo.ScriptVariable();
                model = scriptValue as StructuralModel;
                if (model != null)
                    return true;
            }

            var valueProp = input?.GetType().GetProperty("Value");
            if (valueProp != null && valueProp.GetIndexParameters().Length == 0)
            {
                try
                {
                    var value = valueProp.GetValue(input);
                    model = value as StructuralModel;
                    if (model != null)
                        return true;
                }
                catch
                {
                    // ignored
                }
            }

            return false;
        }

        protected override System.Drawing.Bitmap Icon => Lama.Grasshopper.Properties.Resources.Lama_24x24;
        public override Guid ComponentGuid => new Guid("f1df9d50-39db-4f00-83ea-04f46f4d8a12");
    }
}
