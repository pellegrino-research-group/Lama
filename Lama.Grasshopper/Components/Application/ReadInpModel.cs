using System;
using System.IO;
using System.Linq;
using Grasshopper.Kernel;
using Lama.Core.InputDeck;

namespace Lama.Gh.Components
{
    /// <summary>
    /// Loads a CalculiX/Abaqus-style .inp file into a <see cref="Lama.Core.Model.StructuralModel"/>.
    /// </summary>
    public class ReadInpModelComponent : GH_Component
    {
        private const int MaxRemarksOnCanvas = 30;

        public ReadInpModelComponent()
            : base(
                "ReadInpModel",
                "ReadInp",
                "Deserialize a CalculiX .inp file into a StructuralModel. Unsupported keywords are skipped with remarks.",
                "Lama",
                "Application")
        {
            Message = Name + "\nLama";
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter(
                "File Path",
                "Path",
                "Full path to the .inp file.",
                GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Model", "M", "StructuralModel (Path is set to the .inp file).", GH_ParamAccess.item);
            pManager.AddTextParameter("Warnings", "W", "Parser remarks (unsupported lines, skipped data).", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var path = string.Empty;
            if (!DA.GetData(0, ref path) || string.IsNullOrWhiteSpace(path))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Valid file path required.");
                DA.SetData(0, null);
                DA.SetData(1, string.Empty);
                return;
            }

            path = path.Trim().Trim('"');
            if (!File.Exists(path))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"File not found: {path}");
                DA.SetData(0, null);
                DA.SetData(1, string.Empty);
                return;
            }

            try
            {
                var (model, warnings) = CalculixInputDeckReader.ReadFromFile(path);
                DA.SetData(0, model);
                var joined = warnings.Count == 0
                    ? string.Empty
                    : string.Join(Environment.NewLine, warnings);
                DA.SetData(1, joined);

                if (warnings.Count == 0)
                    return;

                foreach (var w in warnings.Take(MaxRemarksOnCanvas))
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, w);
                if (warnings.Count > MaxRemarksOnCanvas)
                {
                    AddRuntimeMessage(
                        GH_RuntimeMessageLevel.Remark,
                        $"... and {warnings.Count - MaxRemarksOnCanvas} more (see Warnings output).");
                }
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                DA.SetData(0, null);
                DA.SetData(1, string.Empty);
            }
        }

        protected override System.Drawing.Bitmap Icon => Lama.Gh.Properties.Resources.Lama_24x24;

        public override Guid ComponentGuid => new Guid("7f3a9c21-4b0e-4d8f-9c12-88a1f0e3d4c2");
    }
}
