using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Lama.Core.Model.Steps;

namespace Lama.Grasshopper.Components
{
    public class OutputRequestComponent : GH_Component
    {
        public OutputRequestComponent()
            : base("OutputRequest", "Output", "Create a step output request card.", "Lama", "Model")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Type", "T", "0=NodeFile, 1=ElementFile, 2=NodePrint, 3=ElementPrint.", GH_ParamAccess.item, 0);
            pManager.AddTextParameter("Variables", "V", "Output variable tokens (e.g. U,RF,S,E).", GH_ParamAccess.list);
            pManager.AddTextParameter("Target Set", "Set", "Optional NSET/ELSET name (for print types). Leave empty to use all nodes/elements.", GH_ParamAccess.item, string.Empty);
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Request", "R", "StepOutputRequest.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var typeValue = 0;
            var variables = new List<string>();
            var targetSet = "";

            if (!DA.GetData(0, ref typeValue))
                return;
            if (!DA.GetDataList(1, variables))
                return;
            DA.GetData(2, ref targetSet);

            var nonEmptyVars = variables.Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
            if (nonEmptyVars.Length == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "At least one output variable is required.");
                return;
            }

            StepOutputRequest request;
            switch ((StepOutputType)typeValue)
            {
                case StepOutputType.NodeFile:
                    request = StepOutputRequest.NodeFileRaw(nonEmptyVars);
                    break;
                case StepOutputType.ElementFile:
                    request = StepOutputRequest.ElementFileRaw(nonEmptyVars);
                    break;
                case StepOutputType.NodePrint:
                    request = string.IsNullOrWhiteSpace(targetSet)
                        ? StepOutputRequest.NodePrintRaw(nonEmptyVars)
                        : StepOutputRequest.NodePrintRaw(targetSet, nonEmptyVars);
                    break;
                case StepOutputType.ElementPrint:
                    request = string.IsNullOrWhiteSpace(targetSet)
                        ? StepOutputRequest.ElementPrintRaw(nonEmptyVars)
                        : StepOutputRequest.ElementPrintRaw(targetSet, nonEmptyVars);
                    break;
                default:
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Type must be 0..3.");
                    return;
            }

            DA.SetData(0, request);
        }

        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("bbcb7211-26bb-4d88-95dd-d2e53ad5ee57");
    }
}
