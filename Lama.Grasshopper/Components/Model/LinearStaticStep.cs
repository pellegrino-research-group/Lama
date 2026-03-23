using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Lama.Core.Model.Loads;
using Lama.Core.Model.Steps;

namespace Lama.Grasshopper.Components
{
    public class LinearStaticStepComponent : GH_Component
    {
        public LinearStaticStepComponent()
            : base("LinearStaticStep", "Step", "Create a linear static analysis step.", "Lama", "Model")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "Name", "Step name.", GH_ParamAccess.item, "Step-1");
            pManager.AddGenericParameter("Nodal Loads", "L", "Optional list of NodalLoad.", GH_ParamAccess.list);
            pManager[1].Optional = true;
            pManager.AddGenericParameter("Supports (WIP)", "Sup", "WIP: step-level supports are not shipped yet and are currently ignored.", GH_ParamAccess.list);
            pManager[2].Optional = true;
            pManager.AddGenericParameter("Output Requests", "O", "Optional list of StepOutputRequest.", GH_ParamAccess.list);
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Step", "S", "LinearStaticStep.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var name = "Step-1";
            var loadObjects = new List<object>();
            var supportObjects = new List<object>();
            var outputObjects = new List<object>();

            DA.GetData(0, ref name);
            DA.GetDataList(1, loadObjects);
            DA.GetDataList(2, supportObjects);
            DA.GetDataList(3, outputObjects);

            if (supportObjects.Count > 0)
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Warning,
                    "Step-level supports are WIP and not shipped yet. This input is currently ignored; use StructuralModel supports for now.");
            }

            var step = new LinearStaticStep(name);

            foreach (var obj in loadObjects)
            {
                var input = UnwrapInput(obj);
                if (input is NodalLoad load)
                {
                    step.NodalLoads.Add(load);
                    continue;
                }

                if (obj != null)
                {
                    AddRuntimeMessage(
                        GH_RuntimeMessageLevel.Warning,
                        $"Unsupported nodal load input type '{input?.GetType().Name ?? obj.GetType().Name}'.");
                }
            }

            foreach (var obj in outputObjects)
            {
                var input = UnwrapInput(obj);
                if (input is StepOutputRequest output)
                {
                    step.OutputRequests.Add(output);
                    continue;
                }

                if (obj != null)
                {
                    AddRuntimeMessage(
                        GH_RuntimeMessageLevel.Warning,
                        $"Unsupported output request input type '{input?.GetType().Name ?? obj.GetType().Name}'.");
                }
            }

            DA.SetData(0, step);
        }

        private static object UnwrapInput(object input)
        {
            if (input == null)
                return null;

            if (input is IGH_Goo goo)
            {
                var scriptValue = goo.ScriptVariable();
                if (scriptValue != null && !ReferenceEquals(scriptValue, input))
                    return scriptValue;
            }

            var valueProp = input.GetType().GetProperty("Value");
            if (valueProp != null && valueProp.GetIndexParameters().Length == 0)
            {
                try
                {
                    var value = valueProp.GetValue(input);
                    if (value != null && !ReferenceEquals(value, input))
                        return value;
                }
                catch
                {
                    // ignored
                }
            }

            return input;
        }

        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("eb8ef6bc-3fdf-4e9c-af6d-b69f82ef5fcb");
    }
}
