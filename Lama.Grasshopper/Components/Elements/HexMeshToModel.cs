using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Lama.Core.Materials;
using Lama.Core.Model.Sections;
using Lama.Gh;
using Lama.Gh.Definitions;
using Rhino.Geometry;

namespace Lama.Gh.Components
{
    public class HexMeshComponent : GH_Component
    {
        public HexMeshComponent()
            : base(
                "HexMesh",
                "HexMesh",
                "Create a Lama HexMesh definition from Rhino hex meshes (V:8, F:6).",
                "Lama",
                "Elements")
        {
            Message = Name + "\nLama";
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Hex Meshes", "M", "Hex meshes. Each mesh must have V:8 and F:6.", GH_ParamAccess.list);
            pManager.AddTextParameter("Element Set", "Elset", "Element set name.", GH_ParamAccess.item, "E_HEX");
            pManager.AddGenericParameter("Material", "Mat", "Optional material (MaterialBase) used to auto-create a SolidSection.", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddVectorParameter("Orientation Axis 1", "A1", "Optional local material axis-1 direction.", GH_ParamAccess.item, new Vector3d(1, 0, 0));
            pManager[3].Optional = true;
            pManager.AddVectorParameter("Orientation Axis 2", "A2", "Optional in-plane axis-2 direction used with A1 to define local orientation.", GH_ParamAccess.item, new Vector3d(0, 1, 0));
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("HexMesh", "Hex", "Lama HexMesh definition.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Mesh Count", "M", "Number of input hex meshes.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var meshes = new List<Mesh>();
            var elementSet = "E_HEX";
            object materialObj = null;
            var axis1 = new Vector3d(1, 0, 0);
            var axis2 = new Vector3d(0, 1, 0);

            if (!DA.GetDataList(0, meshes) || meshes.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Provide at least one mesh.");
                return;
            }

            DA.GetData(1, ref elementSet);
            DA.GetData(2, ref materialObj);
            var hasAxis1 = DA.GetData(3, ref axis1);
            var hasAxis2 = DA.GetData(4, ref axis2);

            MaterialBase material = null;
            if (materialObj != null)
            {
                if (!TryUnwrapMaterial(materialObj, out material))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Material input must be a Lama MaterialBase.");
                    return;
                }
            }

            SectionOrientation orientation = null;
            if (hasAxis1 || hasAxis2)
            {
                if (!hasAxis1 || !hasAxis2)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Provide both Orientation Axis 1 and Orientation Axis 2, or leave both empty.");
                    return;
                }

                try
                {
                    orientation = new SectionOrientation(
                        axis1.X, axis1.Y, axis1.Z,
                        axis2.X, axis2.Y, axis2.Z);
                }
                catch (Exception ex)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Invalid orientation: {ex.Message}");
                    return;
                }
            }

            try
            {
                var definition = new HexMeshDefinition(meshes, elementSet, material, orientation);
                DA.SetData(0, definition);
                DA.SetData(1, meshes.Count);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        private static bool TryUnwrapMaterial(object input, out MaterialBase material)
        {
            material = input as MaterialBase;
            if (material != null)
                return true;

            if (input is IGH_Goo goo)
            {
                var scriptValue = goo.ScriptVariable();
                material = scriptValue as MaterialBase;
                if (material != null)
                    return true;
            }

            var valueProp = input.GetType().GetProperty("Value");
            if (valueProp != null)
            {
                var value = valueProp.GetValue(input);
                material = value as MaterialBase;
                if (material != null)
                    return true;
            }

            return false;
        }

        protected override Bitmap Icon => Lama.Gh.Properties.Resources.Lama_24x24;

        public override Guid ComponentGuid => new Guid("2f590db4-1ccd-4df4-8f6d-c6f31948570c");
    }
}
