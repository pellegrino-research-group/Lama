using System;
using Grasshopper.Kernel;
using Lama.Core.Materials;
using Lama.Core.Model.Elements;
using Lama.Core.Model.Sections;
using Lama.Grasshopper.Definitions;

namespace Lama.Grasshopper.Components
{
    public class ShellSectionComponent : GH_Component
    {
        public ShellSectionComponent()
            : base("ShellSection", "ShellSec", "Create a shell section assignment (ELSET + Material + Thickness).", "Lama", "Elements")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Element Set", "Elset", "Element set name.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Material", "Mat", "Material (MaterialBase).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Thickness", "t", "Shell thickness.", GH_ParamAccess.item, 0.01);
            pManager.AddGenericParameter("Element Input", "EI", "Optional element input object used to infer ELSET.", GH_ParamAccess.item);
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Section", "Sec", "ShellSection.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string elset = string.Empty;
            object materialObj = null;
            double thickness = 0.01;
            object elementInputObj = null;

            if (!DA.GetData(0, ref elset))
                return;
            if (!DA.GetData(1, ref materialObj))
                return;
            DA.GetData(2, ref thickness);
            DA.GetData(3, ref elementInputObj);

            if (!(materialObj is MaterialBase material))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input Material must be a Lama MaterialBase.");
                return;
            }

            if (!TryResolveElementSetName(elementInputObj, out var inferredElset) && string.IsNullOrWhiteSpace(elset))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Element Set cannot be empty.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(inferredElset))
                elset = inferredElset;

            DA.SetData(0, new ShellSection(elset, material, thickness));
        }

        private static bool TryResolveElementSetName(object input, out string elementSetName)
        {
            elementSetName = null;
            if (input == null)
                return false;

            if (input is HexMeshDefinition hex)
            {
                elementSetName = hex.ElementSetName;
                return !string.IsNullOrWhiteSpace(elementSetName);
            }

            if (input is IElement element)
            {
                elementSetName = element.ElementSetName;
                return !string.IsNullOrWhiteSpace(elementSetName);
            }

            var property = input.GetType().GetProperty("ElementSetName");
            if (property?.PropertyType == typeof(string))
            {
                elementSetName = property.GetValue(input) as string;
                return !string.IsNullOrWhiteSpace(elementSetName);
            }

            return false;
        }

        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("d0f3fcb4-f145-4d42-9773-fd1cc2f7458d");
    }
}
