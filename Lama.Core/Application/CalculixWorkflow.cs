using System;
using System.IO;
using Lama.Core.InputDeck;
using Lama.Core.Materials;
using Lama.Core.Model;
using Lama.Core.Model.Boundary;
using Lama.Core.Model.Elements;
using Lama.Core.Model.Loads;
using Lama.Core.Model.Sections;
using Lama.Core.Model.Steps;

namespace Lama.Core.Application
{
    /// <summary>
    /// Minimal end-to-end workflow helper:
    /// StructuralModel -> .inp -> CalculiX execution.
    /// </summary>
    public static class CalculixWorkflow
    {
        public static StructuralModel CreateMinimalLinearStaticModel()
        {
            var model = new StructuralModel
            {
                Name = "LamaMinimalTetra"
            };

            AddMinimalNodes(model);
            AddMinimalElement(model);
            AddMinimalMaterialAndSection(model);
            AddMinimalBoundaryConditions(model);
            AddMinimalLinearStaticStep(model);

            return model;
        }

        public static string WriteInputDeck(StructuralModel model, string outputDirectory, string jobName)
        {
            ValidateOutputArgs(outputDirectory, jobName);
            Directory.CreateDirectory(outputDirectory);

            var inputPath = Path.Combine(outputDirectory, $"{jobName}.inp");
            var builder = new CalculixInputDeckBuilder();
            builder.WriteToFile(model, inputPath);
            model.Path = inputPath;

            return inputPath;
        }

        public static int BuildAndRunMinimalLinearStatic(
            string executablePath,
            string outputDirectory,
            string jobName = "minimal_linear_static",
            int? numberOfCores = null)
        {
            var model = CreateMinimalLinearStaticModel();
            return BuildAndRun(model, executablePath, outputDirectory, jobName, numberOfCores);
        }

        public static int BuildAndRun(
            StructuralModel model,
            string executablePath,
            string outputDirectory,
            string jobName,
            int? numberOfCores = null)
        {
            var inputPath = WriteInputDeck(model, outputDirectory, jobName);
            return CalculixApplication.RunCalculix(
                executablePath,
                inputPath,
                outputDirectory,
                numberOfCores).ExitCode;
        }

        private static void AddMinimalNodes(StructuralModel model)
        {
            model.Nodes.Add(new Node(1, 0.0, 0.0, 0.0));
            model.Nodes.Add(new Node(2, 1.0, 0.0, 0.0));
            model.Nodes.Add(new Node(3, 0.0, 1.0, 0.0));
            model.Nodes.Add(new Node(4, 0.0, 0.0, 1.0));
        }

        private static void AddMinimalElement(StructuralModel model)
        {
            model.Elements.Add(new Tetra4Element(
                id: 1,
                elementSetName: "E_SOLID",
                nodeIds: new[] { 1, 2, 3, 4 }));
        }

        private static void AddMinimalMaterialAndSection(StructuralModel model)
        {
            var steel = new IsotropicMaterial("MAT_STEEL")
            {
                YoungModulus = 210000.0,
                PoissonRatio = 0.3
            };

            model.Materials.Add(steel);
            model.Sections.Add(new SolidSection("E_SOLID", steel));
        }

        private static void AddMinimalBoundaryConditions(StructuralModel model)
        {
            model.FixedSupports.Add(new FixedSupport(
                name: "BASE_FIX",
                nodeIds: new[] { 1, 2, 3 },
                fixUx: true,
                fixUy: true,
                fixUz: true,
                fixRx: false,
                fixRy: false,
                fixRz: false));
        }

        private static void AddMinimalLinearStaticStep(StructuralModel model)
        {
            var step = new LinearStaticStep("Step-1");
            step.NodalLoads.Add(new NodalLoad(nodeId: 4, dof: StructuralDof.Uz, value: -1000.0));
            model.Steps.Add(step);
        }

        private static void ValidateOutputArgs(string outputDirectory, string jobName)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new ArgumentException("Output directory cannot be empty.", nameof(outputDirectory));
            if (string.IsNullOrWhiteSpace(jobName))
                throw new ArgumentException("Job name cannot be empty.", nameof(jobName));
        }
    }
}
