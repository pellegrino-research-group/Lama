using System;
using Grasshopper.Kernel;
using Lama.Core.Materials;
using Lama.Core.Model.Elements;
using Lama.Core.Model.Sections;
using Lama.Grasshopper.Definitions;

namespace Lama.Grasshopper.Components
{
    public class BeamSectionComponent : GH_Component
    {
        public BeamSectionComponent()
            : base("BeamSection", "BeamSec", "Create a beam section assignment (ELSET + Material + A/Iy/Iz/J).", "Lama", "Elements")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Element Set", "Elset", "Element set name.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Material", "Mat", "Material (MaterialBase).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Area", "A", "Section area.", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Iy", "Iy", "Second moment about local y.", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Iz", "Iz", "Second moment about local z.", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("J", "J", "Torsional constant.", GH_ParamAccess.item, 1.0);
            pManager.AddGenericParameter("Element Input", "EI", "Optional element input object used to infer ELSET.", GH_ParamAccess.item);
            pManager[6].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Section", "Sec", "BeamSection.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string elset = string.Empty;
            object materialObj = null;
            double area = 1.0, iy = 1.0, iz = 1.0, j = 1.0;
            object elementInputObj = null;

            if (!DA.GetData(0, ref elset))
                return;
            if (!DA.GetData(1, ref materialObj))
                return;
            DA.GetData(2, ref area);
            DA.GetData(3, ref iy);
            DA.GetData(4, ref iz);
            DA.GetData(5, ref j);
            DA.GetData(6, ref elementInputObj);

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

            var props = new BeamSectionProperties(area, iy, iz, j);
            DA.SetData(0, new BeamSection(elset, material, props));
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
        public override Guid ComponentGuid => new Guid("0d6b6cab-e285-4b4f-aa6b-d92309ac5864");
    }
}
