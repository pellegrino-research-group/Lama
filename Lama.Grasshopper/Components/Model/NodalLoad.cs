using System;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Lama.Core.Model;
using Lama.Core.Model.Loads;

namespace Lama.Gh.Components
{
    public class NodalLoadComponent : GH_Component
    {
        public NodalLoadComponent()
            : base("NodalLoad", "Load", "Create a nodal load (force/moment).", "Lama", "Model")
        {
            Message = Name + "\nLama";
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Point", "P", "Target point to match to a model node.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("DOF", "D", "Structural DOF index (1..6 or 11).", GH_ParamAccess.item, 3);
            pManager.AddNumberParameter("Value", "V", "Load value.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Nodal Load", "L", "NodalLoad.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var point = Point3d.Unset;
            int dofValue = 3;
            double value = 0.0;

            if (!DA.GetData(0, ref point))
                return;
            DA.GetData(1, ref dofValue);
            if (!DA.GetData(2, ref value))
                return;

            var dof = (StructuralDof)dofValue;
            DA.SetData(0, new NodalLoad(point.X, point.Y, point.Z, dof, value));
        }

        protected override System.Drawing.Bitmap Icon => Lama.Gh.Properties.Resources.Lama_24x24;
        public override Guid ComponentGuid => new Guid("7360dc75-f79f-4417-ab5f-4ec8e5e20750");
    }
}
