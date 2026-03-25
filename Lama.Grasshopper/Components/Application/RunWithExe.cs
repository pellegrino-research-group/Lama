using System;
using System.Drawing;
using System.IO;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Lama.Core.Model;
using Lama.Grasshopper;
using Lama.Core.Application;

namespace Lama.Grasshopper.Components
{
	public class RunWithExeComponent : GH_Component
	{
		public RunWithExeComponent()
			: base(
				"RunWithExe",
				"RunExe",
				"Runs CalculiX using the input deck path attached to a StructuralModel. For CalculiX on Mac, specify the full path (e.g., /usr/local/bin/ccx or /opt/homebrew/bin/ccx)",
				"Lama",
				"Application")
		{
		}

		protected override void RegisterInputParams(GH_InputParamManager pManager)
		{
			pManager.AddGenericParameter("Model", "M", "StructuralModel carrying the input deck path in Model.Path.", GH_ParamAccess.item);
			pManager.AddTextParameter("Executable", "Exe", GetExecutableParamDescription(), GH_ParamAccess.item);
			pManager[1].Optional = true;
			pManager.AddIntegerParameter("Cores", "C", "Number of CPU cores (sets OMP_NUM_THREADS). Optional.", GH_ParamAccess.item);
			pManager[2].Optional = true;
		}

		private string GetExecutableParamDescription()
		{
			if (CalculixApplication.IsMacOS)
			{
				return "Path to CalculiX executable (optional, auto-detects if not provided). Examples: /usr/local/bin/ccx or /opt/homebrew/bin/ccx";
			}
			else if (CalculixApplication.IsWindows)
			{
				return "Path to CalculiX executable (optional, auto-detects if not provided). Example: C:\\CCX\\ccx.exe";
			}
			else
			{
				return "Path to CalculiX executable (optional, auto-detects if not provided). Example: /usr/bin/ccx";
			}
		}

		protected override void RegisterOutputParams(GH_OutputParamManager pManager)
		{
			pManager.AddTextParameter("StdOut", "Out", "Standard output from the process", GH_ParamAccess.item);
			pManager.AddTextParameter("StdErr", "Err", "Standard error from the process", GH_ParamAccess.item);
		}

		protected override void SolveInstance(IGH_DataAccess DA)
		{
			object modelObj = null;
			string inputFilePath = string.Empty;
			string exePath = string.Empty;
			int numberOfCores = 0;

			// Get model input
			if (!DA.GetData(0, ref modelObj))
			{
				DA.SetData(0, string.Empty);
				DA.SetData(1, string.Empty);
				AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Missing model input");
				return;
			}

			if (!TryUnwrapStructuralModel(modelObj, out var model))
			{
				DA.SetData(0, string.Empty);
				DA.SetData(1, string.Empty);
				AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Model input must be a StructuralModel.");
				return;
			}

			inputFilePath = ResolveInputDeckPath(model);

			// Validate input file exists
			if (!File.Exists(inputFilePath))
			{
				DA.SetData(0, string.Empty);
				DA.SetData(1, string.Empty);
				AddRuntimeMessage(
					GH_RuntimeMessageLevel.Error,
					$"Input file not found: {inputFilePath}. Ensure BuildInputDeck has been run and model.Path points to an existing .inp.");
				return;
			}

			// Check for .inp extension
			if (!inputFilePath.EndsWith(".inp", StringComparison.OrdinalIgnoreCase))
			{
				DA.SetData(0, string.Empty);
				DA.SetData(1, string.Empty);
				AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input file must have .inp extension");
				return;
			}

			// Get executable path or auto-detect
			DA.GetData(1, ref exePath);
			bool hasCoresInput = DA.GetData(2, ref numberOfCores);
			if (hasCoresInput && numberOfCores < 1)
			{
				DA.SetData(0, string.Empty);
				DA.SetData(1, string.Empty);
				AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Cores must be >= 1 when provided.");
				return;
			}
			
			if (string.IsNullOrWhiteSpace(exePath))
			{
				// Try to find CalculiX automatically
				exePath = CalculixApplication.FindCalculixExecutable();
				if (string.IsNullOrWhiteSpace(exePath))
				{
					DA.SetData(0, string.Empty);
					DA.SetData(1, string.Empty);
					string platformHint = CalculixApplication.IsMacOS 
						? " Common Mac paths: /usr/local/bin/ccx or /opt/homebrew/bin/ccx" 
						: " Install CalculiX and ensure it's in your PATH";
					AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"CalculiX executable not found.{platformHint}");
					return;
				}
				else
				{
					AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Auto-detected CalculiX at: {exePath}");
				}
			}

			// Validate executable exists
			if (!CalculixApplication.ValidateExecutable(exePath))
			{
				DA.SetData(0, string.Empty);
				DA.SetData(1, string.Empty);
				string platformHint = CalculixApplication.IsMacOS 
					? " Common Mac paths: /usr/local/bin/ccx or /opt/homebrew/bin/ccx" 
					: "";
				AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Executable not found: {exePath}.{platformHint}");
				return;
			}

			// Run CalculiX using the Core Application class
			try
			{
				string workingDirectory = Path.GetDirectoryName(inputFilePath);
				int? selectedCores = hasCoresInput ? numberOfCores : (int?)null;
				int exitCode = CalculixApplication.RunCalculix(exePath, inputFilePath, workingDirectory, selectedCores);

				// Read output files generated by CalculiX
				string baseFileName = Path.GetFileNameWithoutExtension(inputFilePath);
				string outputPath = Path.Combine(workingDirectory, baseFileName + ".dat");
				string errorPath = Path.Combine(workingDirectory, baseFileName + ".sta");

				string stdOut = File.Exists(outputPath) ? File.ReadAllText(outputPath) : $"Exit code: {exitCode}";
				string stdErr = File.Exists(errorPath) ? File.ReadAllText(errorPath) : string.Empty;

				DA.SetData(0, stdOut);
				DA.SetData(1, stdErr);

				if (exitCode == 0)
				{
					AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "CalculiX execution completed successfully");
					if (selectedCores.HasValue)
					{
						AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"OMP_NUM_THREADS set to {selectedCores.Value}");
					}
				}
				else
				{
					AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"CalculiX exited with code: {exitCode}");
				}
			}
			catch (Exception ex)
			{
				DA.SetData(0, string.Empty);
				DA.SetData(1, string.Empty);
				AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Failed to run CalculiX: {ex.Message}");
			}
		}

		private static string ResolveInputDeckPath(StructuralModel model)
		{
			if (model == null || string.IsNullOrWhiteSpace(model.Path))
				return string.Empty;

			var extension = Path.GetExtension(model.Path);
			if (string.Equals(extension, ".inp", StringComparison.OrdinalIgnoreCase))
				return model.Path;

			if (string.Equals(extension, ".dat", StringComparison.OrdinalIgnoreCase))
				return Path.ChangeExtension(model.Path, ".inp");

			return model.Path + ".inp";
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

		protected override Bitmap Icon => Lama.Grasshopper.Properties.Resources.Lama_24x24;

		public override Guid ComponentGuid => new Guid("e5e3f4f9-9a6a-4c05-9da3-b5cc6b61a7a3");
	}
}
