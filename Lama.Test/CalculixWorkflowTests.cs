using System;
using System.IO;
using Lama.Core.Application;
using Lama.Core.InputDeck;
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
    }
}
