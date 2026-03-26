using Lama.Core.Application;
using Lama.Core.InputDeck;
using Xunit;

namespace Lama.Test
{
    public class CalculixInputDeckReaderTests
    {
        [Fact]
        public void ReadFromText_MinimalExportedDeck_RoundTripsCounts()
        {
            var model = CalculixWorkflow.CreateMinimalLinearStaticModel();
            var builder = new CalculixInputDeckBuilder();
            var text = builder.Build(model);

            var (read, warnings) = CalculixInputDeckReader.ReadFromText(text, "minimal.inp");

            Assert.Equal(model.Nodes.Count, read.Nodes.Count);
            Assert.Equal(model.Elements.Count, read.Elements.Count);
            Assert.Equal(model.Materials.Count, read.Materials.Count);
            Assert.Equal(model.Sections.Count, read.Sections.Count);
            Assert.Equal(model.FixedSupports.Count, read.FixedSupports.Count);
            Assert.Equal(model.Steps.Count, read.Steps.Count);
            Assert.Empty(warnings);
        }

        [Fact]
        public void ReadFromText_UnknownKeyword_ProducesWarning()
        {
            var inp = @"*HEADING
test
*NODE
1,0,0,0
*FOO
ignored
";
            var (read, warnings) = CalculixInputDeckReader.ReadFromText(inp, "t.inp");
            Assert.Single(read.Nodes);
            Assert.Contains(warnings, w => w.Contains("FOO", System.StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ReadFromText_C3D8_NotSupported_Warns()
        {
            var inp = @"*NODE
1,0,0,0
2,1,0,0
3,0,1,0
4,0,0,1
5,1,1,0
6,1,0,1
7,0,1,1
8,1,1,1
*ELEMENT,TYPE=C3D8,ELSET=E1
1,1,2,3,4,5,6,7,8
";
            var (read, warnings) = CalculixInputDeckReader.ReadFromText(inp, "hex.inp");
            Assert.Empty(read.Elements);
            Assert.Contains(warnings, w => w.Contains("C3D8", System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
