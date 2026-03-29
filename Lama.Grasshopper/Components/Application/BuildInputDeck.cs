using System;
using System.IO;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Lama.Core.Application;
using Lama.Core.InputDeck;
using Lama.Core.Model;

namespace Lama.Gh.Components
{
    public class BuildInputDeckComponent : GH_Component
    {
        public BuildInputDeckComponent()
            : base("BuildInputDeck", "BuildInp", "Build and optionally write a CalculiX input deck from StructuralModel.", "Lama", "Application")
        {
            Message = Name + "\nLama";
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Model", "M", "StructuralModel.", GH_ParamAccess.item);
            pManager.AddTextParameter("Output Directory", "Dir",
                "Output directory for the .inp file. " +
                "If empty, uses the folder where the .gh file is saved (or a temp folder if unsaved).",
                GH_ParamAccess.item, string.Empty);
            pManager.AddTextParameter("Job Name", "Job", "Job name used for the .inp filename.", GH_ParamAccess.item, "Lama");
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Model", "M", "StructuralModel with written .inp path.", GH_ParamAccess.item);
            pManager.AddTextParameter("Input Deck", "Inp", "Input deck text.", GH_ParamAccess.item);
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

            if (string.IsNullOrWhiteSpace(jobName))
                jobName = "Lama";

            if (!TryUnwrapStructuralModel(modelObj, out var model))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Model input must be a StructuralModel.");
                return;
            }

            if (string.IsNullOrWhiteSpace(outputDirectory))
                outputDirectory = ResolveDefaultDirectory(jobName);

            try
            {
                var builder = new CalculixInputDeckBuilder();
                var deck = builder.Build(model);

                CalculixWorkflow.WriteInputDeck(model, outputDirectory, jobName);
                DA.SetData(0, model);
                DA.SetData(1, deck);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        private string ResolveDefaultDirectory(string jobName)
        {
            var ghFilePath = OnPingDocument()?.FilePath;
            if (!string.IsNullOrWhiteSpace(ghFilePath))
            {
                var ghDir = Path.GetDirectoryName(ghFilePath);
                if (!string.IsNullOrWhiteSpace(ghDir))
                    return Path.Combine(ghDir, jobName);
            }

            return Path.Combine(Path.GetTempPath(), "Lama", jobName);
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

        protected override System.Drawing.Bitmap Icon => Lama.Gh.Properties.Resources.Lama_24x24;
        public override Guid ComponentGuid => new Guid("5ad0fc0c-4cae-4c70-a4e5-fd2d42da7c78");
    }
}
