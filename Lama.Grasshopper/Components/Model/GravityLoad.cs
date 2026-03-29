using System;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Lama.Gh.Components
{
    public class GravityLoadComponent : GH_Component
    {
        public GravityLoadComponent()
            : base("GravityLoad", "Gravity",
                "Create a gravity body load (*DLOAD, GRAV). Uses element density from the assigned material.",
                "Lama", "Model")
        {
            Message = Name + "\nLama";
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Magnitude", "g", "Gravitational acceleration magnitude [m/s²].", GH_ParamAccess.item, 9.81);
            pManager.AddVectorParameter("Direction", "Dir", "Gravity direction vector (will be normalised).", GH_ParamAccess.item, new Vector3d(0, 0, -1));
            pManager.AddTextParameter("Element Set", "Elset", "Target element set name. Leave empty to apply to all elements.", GH_ParamAccess.item, string.Empty);
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Gravity Load", "G", "GravityLoad for use in an analysis step.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double magnitude = 9.81;
            var direction = new Vector3d(0, 0, -1);
            var elementSet = string.Empty;

            DA.GetData(0, ref magnitude);
            DA.GetData(1, ref direction);
            DA.GetData(2, ref elementSet);

            if (direction.Length < 1e-15)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Direction vector must be non-zero.");
                return;
            }

            DA.SetData(0, new Lama.Core.Model.Loads.GravityLoad(
                magnitude, direction.X, direction.Y, direction.Z, elementSet));
        }

        protected override System.Drawing.Bitmap Icon => Lama.Gh.Properties.Resources.Lama_24x24;
        public override Guid ComponentGuid => new Guid("c8a2b7f1-3d6e-4a9b-b5c0-1e7f8d2a9c4b");
    }
}
