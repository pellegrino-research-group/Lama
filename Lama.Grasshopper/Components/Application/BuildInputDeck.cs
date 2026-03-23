using System;
using System.IO;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Lama.Core.Application;
using Lama.Core.InputDeck;
using Lama.Core.Model;

namespace Lama.Grasshopper.Components
{
    public class BuildInputDeckComponent : GH_Component
    {
        public BuildInputDeckComponent()
            : base("BuildInputDeck", "BuildInp", "Build and optionally write a CalculiX input deck from StructuralModel.", "Lama", "Application")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Model", "M", "StructuralModel.", GH_ParamAccess.item);
            pManager.AddTextParameter("Output Directory", "Dir", "Optional output directory for writing .inp.", GH_ParamAccess.item, string.Empty);
            pManager[1].Optional = true;
            pManager.AddTextParameter("Job Name", "Job", "Optional job name for file writing.", GH_ParamAccess.item, "job");
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Deck", "Inp", "Input deck text.", GH_ParamAccess.item);
            pManager.AddTextParameter("Path", "Path", "Written .inp file path (if directory provided).", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            object modelObj = null;
            var outputDirectory = string.Empty;
            var jobName = "job";

            if (!DA.GetData(0, ref modelObj))
                return;
            DA.GetData(1, ref outputDirectory);
            DA.GetData(2, ref jobName);

            if (!TryUnwrapStructuralModel(modelObj, out var model))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Model input must be a StructuralModel.");
                return;
            }

            if (model.Steps.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Model must contain at least one analysis step. Connect a step component (e.g., LinearStaticStep) to StructuralModel.");
                return;
            }

            try
            {
                var builder = new CalculixInputDeckBuilder();
                var deck = builder.Build(model);
                DA.SetData(0, deck);

                if (!string.IsNullOrWhiteSpace(outputDirectory))
                {
                    var path = CalculixWorkflow.WriteInputDeck(model, outputDirectory, jobName);
                    DA.SetData(1, path);
                }
                else
                {
                    DA.SetData(1, string.Empty);
                }
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        private static bool TryUnwrapStructuralModel(object input, out StructuralModel model)
        {
            model = input as StructuralModel;
            if (model != null)
                return true;

            if (input is IGH_Goo goo)
            {
                var scriptValue = goo.ScriptVariable();
                model = scriptValue as StructuralModel;
                if (model != null)
                    return true;
            }

            var valueProp = input?.GetType().GetProperty("Value");
            if (valueProp != null && valueProp.GetIndexParameters().Length == 0)
            {
                try
                {
                    var value = valueProp.GetValue(input);
                    model = value as StructuralModel;
                    if (model != null)
                        return true;
                }
                catch
                {
                    // ignored
                }
            }

            return false;
        }

        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("5ad0fc0c-4cae-4c70-a4e5-fd2d42da7c78");
    }
}
