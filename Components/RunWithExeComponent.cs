using System;
using System.Diagnostics;
using System.IO;
using Grasshopper.Kernel;

namespace Lama.Components
{
	public class RunWithExeComponent : GH_Component
	{
		public RunWithExeComponent()
			: base(
				"RunWithExe",
				"RunExe",
				"Runs an executable with -i <file> argument",
				"Lama",
				"Utils")
		{
		}

		protected override void RegisterInputParams(GH_InputParamManager pManager)
		{
			pManager.AddTextParameter("File", "File", "Path to the input file", GH_ParamAccess.item);
			pManager.AddTextParameter("Executable", "Exe", "Path to the executable (e.g. ccx.exe)", GH_ParamAccess.item);
		}

		protected override void RegisterOutputParams(GH_OutputParamManager pManager)
		{
			pManager.AddTextParameter("StdOut", "Out", "Standard output from the process", GH_ParamAccess.item);
			pManager.AddTextParameter("StdErr", "Err", "Standard error from the process", GH_ParamAccess.item);
		}

		protected override void SolveInstance(IGH_DataAccess DA)
		{
			string inputFilePath = string.Empty;
			string exePath = string.Empty;

			if (!DA.GetData(0, ref inputFilePath))
			{
				DA.SetData(0, string.Empty);
				DA.SetData(1, string.Empty);
				AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Missing input file path");
				return;
			}
			if (!DA.GetData(1, ref exePath))
			{
				DA.SetData(0, string.Empty);
				DA.SetData(1, string.Empty);
				AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Missing executable path");
				return;
			}

			if (string.IsNullOrWhiteSpace(inputFilePath) || !File.Exists(inputFilePath))
			{
				DA.SetData(0, string.Empty);
				DA.SetData(1, string.Empty);
				AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input file not found");
				return;
			}

			if (string.IsNullOrWhiteSpace(exePath))
			{
				DA.SetData(0, string.Empty);
				DA.SetData(1, string.Empty);
				AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid executable path");
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

			try
			{
				// Remove .inp extension for the executable arguments
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(inputFilePath);
				string directoryPath = Path.GetDirectoryName(inputFilePath);
				string filePathWithoutExtension = Path.Combine(directoryPath, fileNameWithoutExtension);

				var startInfo = new ProcessStartInfo
				{
					FileName = exePath,
					Arguments = $"-i \"{filePathWithoutExtension}\"",
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true
				};

				using (var process = new Process { StartInfo = startInfo })
				{
					process.Start();
					string stdOut = process.StandardOutput.ReadToEnd();
					string stdErr = process.StandardError.ReadToEnd();
					process.WaitForExit();

					DA.SetData(0, stdOut ?? string.Empty);
					DA.SetData(1, stdErr ?? string.Empty);

					if (!string.IsNullOrEmpty(stdOut))
						AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, stdOut);
					if (!string.IsNullOrEmpty(stdErr))
						AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, stdErr);
				}
			}
			catch (Exception ex)
			{
				DA.SetData(0, string.Empty);
				DA.SetData(1, string.Empty);
				AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Failed to run: {ex.Message}");
			}
		}

		public override Guid ComponentGuid => new Guid("e5e3f4f9-9a6a-4c05-9da3-b5cc6b61a7a3");
	}
}
