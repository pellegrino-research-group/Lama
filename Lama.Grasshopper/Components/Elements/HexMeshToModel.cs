using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Lama.Core.Materials;
using Lama.Grasshopper.Definitions;
using Rhino.Geometry;

namespace Lama.Grasshopper.Components
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
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Hex Meshes", "M", "Hex meshes. Each mesh must have V:8 and F:6.", GH_ParamAccess.list);
            pManager.AddTextParameter("Element Set", "Elset", "Element set name.", GH_ParamAccess.item, "E_HEX");
            pManager.AddGenericParameter("Material", "Mat", "Optional material (MaterialBase) used to auto-create a SolidSection.", GH_ParamAccess.item);
            pManager[2].Optional = true;
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

            if (!DA.GetDataList(0, meshes) || meshes.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Provide at least one mesh.");
                return;
            }

            DA.GetData(1, ref elementSet);
            DA.GetData(2, ref materialObj);

            MaterialBase material = null;
            if (materialObj != null)
            {
                if (!TryUnwrapMaterial(materialObj, out material))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Material input must be a Lama MaterialBase.");
                    return;
                }
            }

            try
            {
                var definition = new HexMeshDefinition(meshes, elementSet, material);
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

        protected override Bitmap Icon
        {
            get
            {
                try
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    var resourceName = "Lama.Lama.Grasshopper.Resources.Lama_24_24.png";
                    using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                            return new Bitmap(stream);
                    }
                }
                catch
                {
                    // ignored
                }

                return null;
            }
        }

        public override Guid ComponentGuid => new Guid("2f590db4-1ccd-4df4-8f6d-c6f31948570c");
    }
}
