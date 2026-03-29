using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Lama.Gh.Widgets;
using Lama.Core.Model.Loads;
using Lama.Core.Model.Steps;

namespace Lama.Gh.Components
{
    public class StaticStepComponent : GH_SwitcherComponent
    {
        public StaticStepComponent()
            : base("STEP", "STEP",
                "Create a static analysis step (linear or nonlinear).",
                "Lama", "Model")
        {
            Message = Name + "\nLama";
        }

        protected override string DefaultEvaluationUnit => "Linear Static";

        public override string UnitMenuName => "Step Type";

        public override string UnitMenuHeader => "Select step type";

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
        }

        protected override void RegisterEvaluationUnits(EvaluationUnitManager mngr)
        {
            var linear = new EvaluationUnit(
                "Linear Static",
                "Linear Static",
                "Linear static analysis step (*STEP / *STATIC).");
            RegisterCommonInputs(linear);
            RegisterCommonOutputs(linear);
            mngr.RegisterUnit(linear);

            var nonlinear = new EvaluationUnit(
                "Nonlinear Static",
                "Nonlinear Static",
                "Geometrically nonlinear static step (*STEP,NLGEOM / *STATIC). " +
                "Required for plasticity, large deformations, and buckling.");
            RegisterCommonInputs(nonlinear);
            nonlinear.RegisterInputParam(
                new Param_Number(), "Time Period", "T",
                "Total pseudo-time period.",
                GH_ParamAccess.item, new GH_Number(1.0));
            nonlinear.RegisterInputParam(
                new Param_Number(), "Initial Increment", "dt0",
                "Initial time increment.",
                GH_ParamAccess.item, new GH_Number(0.1));
            nonlinear.RegisterInputParam(
                new Param_Number(), "Min Increment", "dtMin",
                "Minimum allowed time increment.",
                GH_ParamAccess.item, new GH_Number(1e-6));
            nonlinear.RegisterInputParam(
                new Param_Number(), "Max Increment", "dtMax",
                "Maximum allowed time increment.",
                GH_ParamAccess.item, new GH_Number(1.0));
            RegisterCommonOutputs(nonlinear);
            mngr.RegisterUnit(nonlinear);
        }

        private static void RegisterCommonInputs(EvaluationUnit unit)
        {
            unit.RegisterInputParam(
                new Param_String(), "Name", "Name", "Step name.",
                GH_ParamAccess.item, new GH_String("Step-1"));
            unit.RegisterInputParam(
                new Param_GenericObject { Optional = true }, "Nodal Loads", "L",
                "Optional list of NodalLoad.",
                GH_ParamAccess.list);
            unit.RegisterInputParam(
                new Param_GenericObject { Optional = true }, "Gravity Load", "G",
                "Optional GravityLoad (*DLOAD, GRAV).",
                GH_ParamAccess.item);
            unit.RegisterInputParam(
                new Param_GenericObject { Optional = true }, "Output Requests", "O",
                "Optional list of StepOutputRequest. " +
                "If empty, defaults to printing all nodal (U, RF, V, A) and element (S, E, PE, PEEQ, ENER, SDV) variables to .dat.",
                GH_ParamAccess.list);
            unit.RegisterInputParam(
                new Param_Boolean(), "Propagate Loads", "Prop",
                "When true (default), loads from previous steps carry over (OP=MOD). " +
                "When false, previous loads are cleared and only this step's loads apply (OP=NEW).",
                GH_ParamAccess.item, new GH_Boolean(true));
        }

        private static void RegisterCommonOutputs(EvaluationUnit unit)
        {
            unit.RegisterOutputParam(
                new Param_GenericObject(), "Step", "S", "Analysis step.");
        }

        protected override void SolveInstance(IGH_DataAccess DA, EvaluationUnit unit)
        {
            var name = "Step-1";
            var loadObjects = new List<object>();
            object gravityObj = null;
            var outputObjects = new List<object>();
            var propagateLoads = true;

            DA.GetData(0, ref name);
            DA.GetDataList(1, loadObjects);
            DA.GetData(2, ref gravityObj);
            DA.GetDataList(3, outputObjects);
            DA.GetData(4, ref propagateLoads);

            AnalysisStepBase step;

            switch (unit.Name)
            {
                case "Linear Static":
                {
                    step = new LinearStaticStep(name);
                    break;
                }
                case "Nonlinear Static":
                {
                    var timePeriod = 1.0;
                    var dt0 = 0.1;
                    var dtMin = 1e-6;
                    var dtMax = 1.0;
                    DA.GetData(5, ref timePeriod);
                    DA.GetData(6, ref dt0);
                    DA.GetData(7, ref dtMin);
                    DA.GetData(8, ref dtMax);

                    step = new NonlinearStaticStep(name)
                    {
                        TimePeriod = timePeriod,
                        InitialIncrement = dt0,
                        MinimumIncrement = dtMin,
                        MaximumIncrement = dtMax
                    };
                    break;
                }
                default:
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Unknown step type.");
                    return;
            }

            foreach (var obj in loadObjects)
            {
                var input = UnwrapInput(obj);
                if (input is NodalLoad load)
                {
                    step.NodalLoads.Add(load);
                    continue;
                }
                if (obj != null)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        $"Unsupported nodal load type '{input?.GetType().Name ?? obj.GetType().Name}'.");
            }

            if (gravityObj != null)
            {
                var unwrapped = UnwrapInput(gravityObj);
                if (unwrapped is GravityLoad gravity)
                    step.GravityLoad = gravity;
                else
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        $"Unsupported gravity load type '{unwrapped?.GetType().Name ?? gravityObj.GetType().Name}'.");
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
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        $"Unsupported output request type '{input?.GetType().Name ?? obj.GetType().Name}'.");
            }

            if (step.OutputRequests.Count == 0)
            {
                step.OutputRequests.Add(StepOutputRequest.NodePrintRaw("U", "RF", "V", "A"));
                step.OutputRequests.Add(StepOutputRequest.ElementPrintRaw("S", "E", "PE", "PEEQ", "ENER", "SDV"));
            }

            step.PropagateLoads = propagateLoads;
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

        protected override System.Drawing.Bitmap Icon => Lama.Gh.Properties.Resources.Lama_24x24;
        public override Guid ComponentGuid => new Guid("eb8ef6bc-3fdf-4e9c-af6d-b69f82ef5fcb");
    }
}
