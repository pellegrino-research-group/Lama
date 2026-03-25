using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Lama.Core.Model.Boundary;
using Rhino.Geometry;

namespace Lama.Grasshopper.Components
{
    public class FixedSupportComponent : GH_Component
    {
        public FixedSupportComponent()
            : base("FixedSupport", "Fix", "Create a fixed support constraint.", "Lama", "Model")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "Name", "Support name.", GH_ParamAccess.item, "Support");
            pManager.AddPointParameter("Points", "P", "Target points to match to model nodes.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Fix Translations", "T", "Fix Ux,Uy,Uz.", GH_ParamAccess.item, true);
            pManager.AddBooleanParameter("Fix Rotations", "R", "Fix Rx,Ry,Rz.", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Support", "S", "FixedSupport.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var name = "Support";
            var points = new List<Point3d>();
            var fixTranslations = true;
            var fixRotations = false;

            DA.GetData(0, ref name);
            if (!DA.GetDataList(1, points))
                return;
            DA.GetData(2, ref fixTranslations);
            DA.GetData(3, ref fixRotations);

            var targets = new List<FixedSupport.SupportPointTarget>(points.Count);
            foreach (var p in points)
                targets.Add(new FixedSupport.SupportPointTarget(p.X, p.Y, p.Z));

            DA.SetData(0, new FixedSupport(name, targets, fixTranslations, fixRotations));
        }

        protected override System.Drawing.Bitmap Icon => Lama.Grasshopper.Properties.Resources.Lama_24x24;
        public override Guid ComponentGuid => new Guid("afecdf4d-4324-4521-92dc-e08e9252dabf");
    }
}
