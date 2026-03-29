using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Lama.Core.Model.Boundary;
using Lama.Gh.Widgets;
using Rhino.Geometry;

namespace Lama.Gh.Components
{
    public class FixedSupportComponent : GH_ExtendableComponent
    {
        private MenuCheckBox _cbUx;
        private MenuCheckBox _cbUy;
        private MenuCheckBox _cbUz;
        private MenuCheckBox _cbRx;
        private MenuCheckBox _cbRy;
        private MenuCheckBox _cbRz;

        public FixedSupportComponent()
            : base("FixedSupport", "Fix",
                "Create a support with per-DOF fixes (CalculiX *BOUNDARY 1–6). " +
                "Optional DOF string e.g. 001100 (Ux,Uy,Uz,Rx,Ry,Rz); empty = use menu checkboxes.",
                "Lama", "Model")
        {
            Message = Name + "\nLama";
        }

        protected override void Setup(GH_ExtendableComponentAttributes attr)
        {
            var menu = new GH_ExtendableMenu(0, "dof_menu") { Name = "Fixed DOFs" };
            var row = new MenuHorizontalPanel(0, "dof_row");

            _cbUx = new MenuCheckBox(0, "ux", "Ux") { Active = true, TagPosition = MenuCheckBox.TagPlacement.Above };
            _cbUy = new MenuCheckBox(1, "uy", "Uy") { Active = true, TagPosition = MenuCheckBox.TagPlacement.Above };
            _cbUz = new MenuCheckBox(2, "uz", "Uz") { Active = true, TagPosition = MenuCheckBox.TagPlacement.Above };
            _cbRx = new MenuCheckBox(3, "rx", "Rx") { Active = true, TagPosition = MenuCheckBox.TagPlacement.Above };
            _cbRy = new MenuCheckBox(4, "ry", "Ry") { Active = true, TagPosition = MenuCheckBox.TagPlacement.Above };
            _cbRz = new MenuCheckBox(5, "rz", "Rz") { Active = true, TagPosition = MenuCheckBox.TagPlacement.Above };

            _cbUx.ValueChanged += OnDofUiChanged;
            _cbUy.ValueChanged += OnDofUiChanged;
            _cbUz.ValueChanged += OnDofUiChanged;
            _cbRx.ValueChanged += OnDofUiChanged;
            _cbRy.ValueChanged += OnDofUiChanged;
            _cbRz.ValueChanged += OnDofUiChanged;

            row.AddControl(_cbUx);
            row.AddControl(_cbUy);
            row.AddControl(_cbUz);
            row.AddControl(_cbRx);
            row.AddControl(_cbRy);
            row.AddControl(_cbRz);

            menu.AddControl(row);
            menu.Expand();
            attr.AddMenu(menu);
        }

        private void OnDofUiChanged(object sender, EventArgs e)
        {
            ExpireSolution(true);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "Name", "Support name.", GH_ParamAccess.item, "Support");
            pManager.AddPointParameter("Points", "P", "Target points to match to model nodes.", GH_ParamAccess.list);
            pManager.AddTextParameter("DOF", "DOF",
                "Optional. Exactly six characters, only 0 or 1: Ux,Uy,Uz,Rx,Ry,Rz (e.g. 001100). Whitespace ignored. Overrides menu when set.",
                GH_ParamAccess.item, string.Empty);
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Support", "S", "FixedSupport.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var name = "Support";
            var points = new List<Point3d>();
            var dofMask = string.Empty;

            DA.GetData(0, ref name);
            if (!DA.GetDataList(1, points))
                return;
            DA.GetData(2, ref dofMask);

            bool ux, uy, uz, rx, ry, rz;
            if (TryParseDofMask(dofMask, out ux, out uy, out uz, out rx, out ry, out rz))
            {
                // wired DOF string overrides menu
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(dofMask))
                {
                    AddRuntimeMessage(
                        GH_RuntimeMessageLevel.Warning,
                        "DOF must be exactly six 0/1 characters (Ux,Uy,Uz,Rx,Ry,Rz). Using menu checkboxes.");
                }

                ux = _cbUx.Active;
                uy = _cbUy.Active;
                uz = _cbUz.Active;
                rx = _cbRx.Active;
                ry = _cbRy.Active;
                rz = _cbRz.Active;
            }

            if (!ux && !uy && !uz && !rx && !ry && !rz)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No DOF is fixed; support has no effect in CalculiX.");
            }

            var targets = new List<FixedSupport.SupportPointTarget>(points.Count);
            foreach (var p in points)
                targets.Add(new FixedSupport.SupportPointTarget(p.X, p.Y, p.Z));

            DA.SetData(0, new FixedSupport(name, targets, ux, uy, uz, rx, ry, rz));
        }

        private static bool TryParseDofMask(string text, out bool ux, out bool uy, out bool uz, out bool rx, out bool ry, out bool rz)
        {
            ux = uy = uz = rx = ry = rz = false;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var compact = new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
            if (compact.Length != 6)
                return false;

            foreach (var c in compact)
            {
                if (c != '0' && c != '1')
                    return false;
            }

            ux = compact[0] == '1';
            uy = compact[1] == '1';
            uz = compact[2] == '1';
            rx = compact[3] == '1';
            ry = compact[4] == '1';
            rz = compact[5] == '1';
            return true;
        }

        protected override System.Drawing.Bitmap Icon => Lama.Gh.Properties.Resources.Lama_24x24;
        public override Guid ComponentGuid => new Guid("afecdf4d-4324-4521-92dc-e08e9252dabf");
    }
}
