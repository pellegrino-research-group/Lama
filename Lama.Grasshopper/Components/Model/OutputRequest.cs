using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Lama.Gh.Widgets;
using Lama.Core.Model.Steps;

namespace Lama.Gh.Components
{
    public class OutputRequestComponent : GH_SwitcherComponent
    {
        private MenuCheckBox _printCheckNode;
        private MenuCheckBox _cbNodeU;
        private MenuCheckBox _cbNodeRF;
        private MenuCheckBox _cbNodeV;
        private MenuCheckBox _cbNodeA;

        private MenuCheckBox _printCheckElement;
        private MenuCheckBox _cbElemS;
        private MenuCheckBox _cbElemE;
        private MenuCheckBox _cbElemPE;
        private MenuCheckBox _cbElemPEEQ;
        private MenuCheckBox _cbElemENER;
        private MenuCheckBox _cbElemSDV;

        public OutputRequestComponent()
            : base("OutputRequest", "Output",
                "Create a step output request card. Switch between Node and Element output.",
                "Lama", "Model")
        {
            Message = Name + "\nLama";
        }

        protected override string DefaultEvaluationUnit => "Node Output";

        public override string UnitMenuName => "Output Scope";

        public override string UnitMenuHeader => "Select output scope";

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
        }

        protected override void RegisterEvaluationUnits(EvaluationUnitManager mngr)
        {
            RegisterNodeUnit(mngr);
            RegisterElementUnit(mngr);
        }

        private void RegisterNodeUnit(EvaluationUnitManager mngr)
        {
            var unit = new EvaluationUnit(
                "Node Output",
                "Node Output",
                "Nodal output request (*NODE FILE or *NODE PRINT).");

            unit.RegisterInputParam(
                new Param_String(), "Target Set", "Set",
                "NSET name for *NODE PRINT. Leave empty for all nodes.",
                GH_ParamAccess.item, new GH_String(string.Empty));

            unit.RegisterOutputParam(
                new Param_GenericObject(), "Request", "R", "StepOutputRequest.");

            var variablesMenu = new GH_ExtendableMenu(0, "node_vars");
            variablesMenu.Name = "Variables";

            var panel = new MenuPanel(0, "node_panel");
            _cbNodeU = new MenuCheckBox(0, "U", "U  (Displacements)") { Active = true };
            _cbNodeRF = new MenuCheckBox(1, "RF", "RF (Reaction forces)") { Active = true };
            _cbNodeV = new MenuCheckBox(2, "V", "V  (Velocities)") { Active = false };
            _cbNodeA = new MenuCheckBox(3, "A", "A  (Accelerations)") { Active = false };

            _cbNodeU.ValueChanged += OnCheckChanged;
            _cbNodeRF.ValueChanged += OnCheckChanged;
            _cbNodeV.ValueChanged += OnCheckChanged;
            _cbNodeA.ValueChanged += OnCheckChanged;

            panel.AddControl(_cbNodeU);
            panel.AddControl(_cbNodeRF);
            panel.AddControl(_cbNodeV);
            panel.AddControl(_cbNodeA);
            variablesMenu.AddControl(panel);

            var optionsMenu = new GH_ExtendableMenu(1, "node_opts");
            optionsMenu.Name = "Options";

            var optPanel = new MenuPanel(1, "node_opt_panel");
            _printCheckNode = new MenuCheckBox(10, "Print", "Print to .dat") { Active = true };
            _printCheckNode.ValueChanged += OnCheckChanged;
            optPanel.AddControl(_printCheckNode);
            optionsMenu.AddControl(optPanel);

            variablesMenu.Expand();
            optionsMenu.Expand();
            unit.AddMenu(variablesMenu);
            unit.AddMenu(optionsMenu);

            mngr.RegisterUnit(unit);
        }

        private void RegisterElementUnit(EvaluationUnitManager mngr)
        {
            var unit = new EvaluationUnit(
                "Element Output",
                "Element Output",
                "Element output request (*EL FILE or *EL PRINT).");

            unit.RegisterInputParam(
                new Param_String(), "Target Set", "Set",
                "ELSET name for *EL PRINT. Leave empty for all elements.",
                GH_ParamAccess.item, new GH_String(string.Empty));

            unit.RegisterOutputParam(
                new Param_GenericObject(), "Request", "R", "StepOutputRequest.");

            var variablesMenu = new GH_ExtendableMenu(0, "elem_vars");
            variablesMenu.Name = "Variables";

            var panel = new MenuPanel(0, "elem_panel");
            _cbElemS = new MenuCheckBox(0, "S", "S     (Stresses)") { Active = true };
            _cbElemE = new MenuCheckBox(1, "E", "E     (Strains)") { Active = false };
            _cbElemPE = new MenuCheckBox(2, "PE", "PE   (Plastic strains)") { Active = false };
            _cbElemPEEQ = new MenuCheckBox(3, "PEEQ", "PEEQ (Equiv. plastic strain)") { Active = false };
            _cbElemENER = new MenuCheckBox(4, "ENER", "ENER (Strain energy density)") { Active = false };
            _cbElemSDV = new MenuCheckBox(5, "SDV", "SDV  (State variables)") { Active = false };

            _cbElemS.ValueChanged += OnCheckChanged;
            _cbElemE.ValueChanged += OnCheckChanged;
            _cbElemPE.ValueChanged += OnCheckChanged;
            _cbElemPEEQ.ValueChanged += OnCheckChanged;
            _cbElemENER.ValueChanged += OnCheckChanged;
            _cbElemSDV.ValueChanged += OnCheckChanged;

            panel.AddControl(_cbElemS);
            panel.AddControl(_cbElemE);
            panel.AddControl(_cbElemPE);
            panel.AddControl(_cbElemPEEQ);
            panel.AddControl(_cbElemENER);
            panel.AddControl(_cbElemSDV);
            variablesMenu.AddControl(panel);

            var optionsMenu = new GH_ExtendableMenu(1, "elem_opts");
            optionsMenu.Name = "Options";

            var optPanel = new MenuPanel(1, "elem_opt_panel");
            _printCheckElement = new MenuCheckBox(10, "Print", "Print to .dat") { Active = true };
            _printCheckElement.ValueChanged += OnCheckChanged;
            optPanel.AddControl(_printCheckElement);
            optionsMenu.AddControl(optPanel);

            variablesMenu.Expand();
            optionsMenu.Expand();
            unit.AddMenu(variablesMenu);
            unit.AddMenu(optionsMenu);

            mngr.RegisterUnit(unit);
        }

        private void OnCheckChanged(object sender, EventArgs e)
        {
            ExpireSolution(true);
        }

        protected override void SolveInstance(IGH_DataAccess DA, EvaluationUnit unit)
        {
            var targetSet = string.Empty;
            DA.GetData(0, ref targetSet);

            StepOutputRequest request;

            switch (unit.Name)
            {
                case "Node Output":
                    request = BuildNodeRequest(targetSet);
                    break;
                case "Element Output":
                    request = BuildElementRequest(targetSet);
                    break;
                default:
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Unknown output scope.");
                    return;
            }

            if (request == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No output variables selected.");
                return;
            }

            DA.SetData(0, request);
        }

        private StepOutputRequest BuildNodeRequest(string targetSet)
        {
            var vars = new List<string>();
            if (_cbNodeU.Active) vars.Add("U");
            if (_cbNodeRF.Active) vars.Add("RF");
            if (_cbNodeV.Active) vars.Add("V");
            if (_cbNodeA.Active) vars.Add("A");

            if (vars.Count == 0)
                return null;

            var variables = vars.ToArray();

            if (_printCheckNode.Active)
            {
                return string.IsNullOrWhiteSpace(targetSet)
                    ? StepOutputRequest.NodePrintRaw(variables)
                    : StepOutputRequest.NodePrintRaw(targetSet, variables);
            }

            return StepOutputRequest.NodeFileRaw(variables);
        }

        private StepOutputRequest BuildElementRequest(string targetSet)
        {
            var vars = new List<string>();
            if (_cbElemS.Active) vars.Add("S");
            if (_cbElemE.Active) vars.Add("E");
            if (_cbElemPE.Active) vars.Add("PE");
            if (_cbElemPEEQ.Active) vars.Add("PEEQ");
            if (_cbElemENER.Active) vars.Add("ENER");
            if (_cbElemSDV.Active) vars.Add("SDV");

            if (vars.Count == 0)
                return null;

            var variables = vars.ToArray();

            if (_printCheckElement.Active)
            {
                return string.IsNullOrWhiteSpace(targetSet)
                    ? StepOutputRequest.ElementPrintRaw(variables)
                    : StepOutputRequest.ElementPrintRaw(targetSet, variables);
            }

            return StepOutputRequest.ElementFileRaw(variables);
        }

        protected override System.Drawing.Bitmap Icon => Lama.Gh.Properties.Resources.Lama_24x24;
        public override Guid ComponentGuid => new Guid("bbcb7211-26bb-4d88-95dd-d2e53ad5ee57");
    }
}
