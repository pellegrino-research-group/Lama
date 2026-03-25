using System;
using System.IO;
using Lama.Core.Application;
using Lama.Core.InputDeck;
using Lama.Core.Materials;
using Lama.Core.Model;
using Lama.Core.Model.Boundary;
using Lama.Core.Model.Elements;
using Lama.Core.Model.Loads;
using Lama.Core.Model.Sections;
using Lama.Core.Model.Steps;
using Xunit;

namespace Lama.Test
{
    public class CalculixWorkflowTests
    {
        [Fact]
        public void CreateMinimalLinearStaticModel_ShouldContainRequiredEntities()
        {
            var model = CalculixWorkflow.CreateMinimalLinearStaticModel();

            Assert.Equal(4, model.Nodes.Count);
            Assert.Single(model.Elements);
            Assert.Single(model.Materials);
            Assert.Single(model.Sections);
            Assert.Single(model.FixedSupports);
            Assert.Single(model.Steps);
        }

        [Fact]
        public void BuildInputDeck_ShouldContainExpectedCards()
        {
            var model = CalculixWorkflow.CreateMinimalLinearStaticModel();
            var builder = new CalculixInputDeckBuilder();

            var deck = builder.Build(model);

            Assert.Contains("*NODE", deck);
            Assert.Contains("*ELEMENT,TYPE=C3D4,ELSET=E_SOLID", deck);
            Assert.Contains("*MATERIAL,NAME=MAT_STEEL", deck);
            Assert.Contains("*SOLID SECTION,ELSET=E_SOLID,MATERIAL=MAT_STEEL", deck);
            Assert.Contains("*BOUNDARY", deck);
            Assert.Contains("*STEP", deck);
            Assert.Contains("*STATIC", deck);
            Assert.Contains("*CLOAD", deck);
            Assert.Contains("*END STEP", deck);
        }

        [Fact]
        public void WriteInputDeck_ShouldCreateInpFile()
        {
            var model = CalculixWorkflow.CreateMinimalLinearStaticModel();
            var directory = Path.Combine(Path.GetTempPath(), "lama-test-" + Guid.NewGuid().ToString("N"));

            try
            {
                var inpPath = CalculixWorkflow.WriteInputDeck(model, directory, "job");

                Assert.True(File.Exists(inpPath));
                Assert.Equal(inpPath, model.Path);
                var content = File.ReadAllText(inpPath);
                Assert.Contains("*HEADING", content);
                Assert.Contains("LamaMinimalTetra", content);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void NodePrint_WithExplicitNset_ShouldWriteNsetOnKeyword()
        {
            var model = CreateMinimalModelWithStep(step =>
            {
                step.OutputRequests.Add(StepOutputRequest.NodePrintRaw("MY_NSET", "U"));
            });

            var deck = new CalculixInputDeckBuilder().Build(model);

            Assert.Contains("*NODE PRINT,NSET=MY_NSET", deck);
            Assert.DoesNotContain("*NSET,NSET=NALL", deck);
        }

        [Fact]
        public void NodePrint_WithoutNset_ShouldFallBackToNall()
        {
            var model = CreateMinimalModelWithStep(step =>
            {
                step.OutputRequests.Add(StepOutputRequest.NodePrintRaw(new[] { "U" }));
            });

            var deck = new CalculixInputDeckBuilder().Build(model);

            Assert.Contains("*NODE PRINT,NSET=NALL", deck);
            Assert.Contains("*NSET,NSET=NALL", deck);
        }

        [Fact]
        public void ElementPrint_WithExplicitElset_ShouldWriteElsetOnKeyword()
        {
            var model = CreateMinimalModelWithStep(step =>
            {
                step.OutputRequests.Add(StepOutputRequest.ElementPrintRaw("E_SOLID", "S"));
            });

            var deck = new CalculixInputDeckBuilder().Build(model);

            Assert.Contains("*EL PRINT,ELSET=E_SOLID", deck);
            Assert.DoesNotContain("*ELSET,ELSET=EALL", deck);
        }

        [Fact]
        public void ElementPrint_WithoutElset_ShouldFallBackToEall()
        {
            var model = CreateMinimalModelWithStep(step =>
            {
                step.OutputRequests.Add(StepOutputRequest.ElementPrintRaw(new[] { "S" }));
            });

            var deck = new CalculixInputDeckBuilder().Build(model);

            Assert.Contains("*EL PRINT,ELSET=EALL", deck);
            Assert.Contains("*ELSET,ELSET=EALL", deck);
        }

        private static StructuralModel CreateMinimalModelWithStep(Action<LinearStaticStep> configureStep)
        {
            var model = new StructuralModel { Name = "TestModel" };
            model.Nodes.Add(new Node(1, 0, 0, 0));
            model.Nodes.Add(new Node(2, 1, 0, 0));
            model.Nodes.Add(new Node(3, 0, 1, 0));
            model.Nodes.Add(new Node(4, 0, 0, 1));

            model.Elements.Add(new Tetra4Element(1, "E_SOLID", new[] { 1, 2, 3, 4 }));

            var mat = new IsotropicMaterial("MAT") { YoungModulus = 210000, PoissonRatio = 0.3 };
            model.Materials.Add(mat);
            model.Sections.Add(new SolidSection("E_SOLID", mat));

            model.FixedSupports.Add(new FixedSupport("FIX", new[] { 1, 2, 3 },
                fixUx: true, fixUy: true, fixUz: true, fixRx: false, fixRy: false, fixRz: false));

            var step = new LinearStaticStep("Step-1");
            step.NodalLoads.Add(new NodalLoad(4, StructuralDof.Uz, -1000));
            configureStep(step);
            model.Steps.Add(step);

            return model;
        }
    }
}
