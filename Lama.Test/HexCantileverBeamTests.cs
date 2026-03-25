using System;
using System.IO;
using System.Linq;
using Lama.Core.Application;
using Lama.Core.InputDeck;
using Lama.Core.Materials;
using Lama.Core.Model;
using Lama.Core.Model.Boundary;
using Lama.Core.Model.Elements;
using Lama.Core.Model.Loads;
using Lama.Core.Model.Sections;
using Lama.Core.Model.Steps;
using Lama.Core.PostProcessing;
using Xunit;

namespace Lama.Test
{
    public class HexCantileverBeamTests
    {
        [Fact]
        public void BuildInputDeck_HexCantileverWithTipLoad_ShouldContainExpectedCards()
        {
            var model = CreateHexCantileverBeamModel();
            var builder = new CalculixInputDeckBuilder();

            var deck = builder.Build(model);

            Assert.Contains("*NODE", deck);
            Assert.Contains("*ELEMENT,TYPE=C3D20R,ELSET=E_BEAM", deck);
            Assert.Contains("*MATERIAL,NAME=MAT_STEEL", deck);
            Assert.Contains("*SOLID SECTION,ELSET=E_BEAM,MATERIAL=MAT_STEEL", deck);
            Assert.Contains("*BOUNDARY", deck);
            Assert.Contains("*CLOAD", deck);
            Assert.Contains("2,3,-1000", deck);
            Assert.Contains("*NODE FILE", deck);
            Assert.Contains("U,RF", deck);
            Assert.Contains("*NODE PRINT,NSET=SUPPORT_1_ROOT_FIX", deck);
            Assert.Contains("*EL FILE", deck);
            Assert.Contains("S,E,PEEQ", deck);
            Assert.Contains("*EL PRINT,ELSET=E_BEAM", deck);
            Assert.Contains("S,E", deck);
            Assert.Contains("*END STEP", deck);
        }

        [Fact]
        public void BuildAndRun_HexCantileverWithTipLoad_ShouldGenerateResultFiles()
        {
            if (!ShouldRunCalculixIntegration())
            {
                // Enable with LAMA_RUN_CALCULIX_INTEGRATION=1.
                return;
            }

            var executablePath = ResolveCalculixExecutable();
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                // Integration test requires a local CalculiX installation.
                return;
            }

            var model = CreateHexCantileverBeamModel();
            var jobName = "hex_cantilever";
            var outputDirectory = CreatePersistentProjectOutputDirectory();

            var exitCode = CalculixWorkflow.BuildAndRun(
                model,
                executablePath,
                outputDirectory,
                jobName,
                numberOfCores: 1);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(Path.Combine(outputDirectory, $"{jobName}.inp")));
            var datPath = Path.Combine(outputDirectory, $"{jobName}.dat");
            Assert.True(File.Exists(datPath));

            var tables = CalculixDatParser.ParseFile(datPath);
            Assert.NotEmpty(tables);
        }

        private static string ResolveCalculixExecutable()
        {
            var fromEnvironment = Environment.GetEnvironmentVariable("CCX_EXE");
            if (!string.IsNullOrWhiteSpace(fromEnvironment))
                return fromEnvironment;

            return CalculixApplication.FindCalculixExecutable();
        }

        private static bool ShouldRunCalculixIntegration()
        {
            var runFlag = Environment.GetEnvironmentVariable("LAMA_RUN_CALCULIX_INTEGRATION");
            return string.Equals(runFlag, "1", StringComparison.Ordinal);
        }

        private static string CreatePersistentProjectOutputDirectory()
        {
            var root = FindRepositoryRoot(Directory.GetCurrentDirectory());
            var outputDirectory = Path.Combine(
                root,
                "Lama.Test",
                "generated",
                "hex_cantilever",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));

            Directory.CreateDirectory(outputDirectory);
            return outputDirectory;
        }

        private static string FindRepositoryRoot(string startDirectory)
        {
            var current = new DirectoryInfo(startDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "Lama.sln")))
                    return current.FullName;

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate repository root containing Lama.sln.");
        }

        private static StructuralModel CreateHexCantileverBeamModel()
        {
            var model = new StructuralModel
            {
                Name = "HexCantileverBeam"
            };

            // 20-node hexahedral beam block: x in [0, 10], y in [0, 1], z in [0, 1]
            model.Nodes.Add(new Node(1, 0.0, 0.0, 0.0));
            model.Nodes.Add(new Node(2, 10.0, 0.0, 0.0));
            model.Nodes.Add(new Node(3, 10.0, 1.0, 0.0));
            model.Nodes.Add(new Node(4, 0.0, 1.0, 0.0));
            model.Nodes.Add(new Node(5, 0.0, 0.0, 1.0));
            model.Nodes.Add(new Node(6, 10.0, 0.0, 1.0));
            model.Nodes.Add(new Node(7, 10.0, 1.0, 1.0));
            model.Nodes.Add(new Node(8, 0.0, 1.0, 1.0));
            model.Nodes.Add(new Node(9, 5.0, 0.0, 0.0));
            model.Nodes.Add(new Node(10, 10.0, 0.5, 0.0));
            model.Nodes.Add(new Node(11, 5.0, 1.0, 0.0));
            model.Nodes.Add(new Node(12, 0.0, 0.5, 0.0));
            model.Nodes.Add(new Node(13, 5.0, 0.0, 1.0));
            model.Nodes.Add(new Node(14, 10.0, 0.5, 1.0));
            model.Nodes.Add(new Node(15, 5.0, 1.0, 1.0));
            model.Nodes.Add(new Node(16, 0.0, 0.5, 1.0));
            model.Nodes.Add(new Node(17, 0.0, 0.0, 0.5));
            model.Nodes.Add(new Node(18, 10.0, 0.0, 0.5));
            model.Nodes.Add(new Node(19, 10.0, 1.0, 0.5));
            model.Nodes.Add(new Node(20, 0.0, 1.0, 0.5));

            model.Elements.Add(new Hexa20Element(
                id: 1,
                elementSetName: "E_BEAM",
                nodeIds: new[]
                {
                    1, 2, 3, 4, 5, 6, 7, 8,
                    9, 10, 11, 12, 13, 14, 15, 16,
                    17, 18, 19, 20
                }));

            var steel = new IsotropicMaterial("MAT_STEEL")
            {
                YoungModulus = 210000.0,
                PoissonRatio = 0.3
            };

            model.Materials.Add(steel);
            model.Sections.Add(new SolidSection("E_BEAM", steel));

            // Cantilever root at x=0 face.
            model.FixedSupports.Add(new FixedSupport(
                name: "ROOT_FIX",
                nodeIds: new[] { 1, 4, 5, 8, 12, 16, 17, 20 },
                fixUx: true,
                fixUy: true,
                fixUz: true,
                fixRx: false,
                fixRy: false,
                fixRz: false));

            var step = new LinearStaticStep("Step-1");
            step.NodalLoads.Add(new NodalLoad(nodeId: 2, dof: StructuralDof.Uz, value: -1000.0));
            step.OutputRequests.Add(StepOutputRequest.NodeFile(NodalOutputVariable.U, NodalOutputVariable.RF));
            step.OutputRequests.Add(StepOutputRequest.NodePrint("SUPPORT_1_ROOT_FIX", NodalOutputVariable.RF));
            step.OutputRequests.Add(StepOutputRequest.ElementFile(
                ElementOutputVariable.S,
                ElementOutputVariable.E,
                ElementOutputVariable.PEEQ));
            step.OutputRequests.Add(StepOutputRequest.ElementPrint("E_BEAM",
                ElementOutputVariable.S,
                ElementOutputVariable.E));
            model.Steps.Add(step);

            return model;
        }
    }
}
