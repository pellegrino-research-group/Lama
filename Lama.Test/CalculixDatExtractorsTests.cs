using Lama.Core.PostProcessing;
using Xunit;

namespace Lama.Test
{
    public class CalculixDatExtractorsTests
    {
        [Fact]
        public void TryGetNodalDisplacements_ShouldReturnTypedVectors()
        {
            const string datText = @"
 displacements for set all
 node u1 u2 u3
 1 0.0 0.0 -1.0E-03
 2 1.0E-04 0.0 -2.0E-03
";

            var tables = CalculixDatParser.ParseText(datText);
            var found = CalculixDatExtractors.TryGetNodalDisplacements(tables, out var displacements);

            Assert.True(found);
            Assert.Equal(2, displacements.Count);
            Assert.Equal(1, displacements[0].NodeId);
            Assert.Equal(-1.0E-03, displacements[0].Z, 12);
        }

        [Fact]
        public void TryGetNodalReactions_ShouldReturnTypedVectors()
        {
            const string datText = @"
 reaction forces for set support
 node rf1 rf2 rf3
 10 100.0 0.0 -500.0
";

            var tables = CalculixDatParser.ParseText(datText);
            var found = CalculixDatExtractors.TryGetNodalReactions(tables, out var reactions);

            Assert.True(found);
            Assert.Single(reactions);
            Assert.Equal(10, reactions[0].NodeId);
            Assert.Equal(100.0, reactions[0].Fx, 12);
            Assert.Equal(-500.0, reactions[0].Fz, 12);
            Assert.Equal(0.0, reactions[0].Mx, 12);
            Assert.Equal(0.0, reactions[0].Mz, 12);
        }

        [Fact]
        public void TryGetNodalReactions_ShouldParseSixComponents_AsForceAndMoment()
        {
            const string datText = @"
 reaction forces and moments for set support
 node rf1 rf2 rf3 rf4 rf5 rf6
 5 10.0 0.0 0.0 0.0 20.0 -30.0
";

            var tables = CalculixDatParser.ParseText(datText);
            var found = CalculixDatExtractors.TryGetNodalReactions(tables, out var reactions);

            Assert.True(found);
            Assert.Single(reactions);
            Assert.Equal(5, reactions[0].NodeId);
            Assert.Equal(10.0, reactions[0].Fx, 12);
            Assert.Equal(20.0, reactions[0].My, 12);
            Assert.Equal(-30.0, reactions[0].Mz, 12);
        }

        [Fact]
        public void TryGetNodalReactions_ShouldMatchRfColumnHeader_WhenTitleOmitsReactionWord()
        {
            const string datText = @"
 nodal output for set support
 node rf1 rf2 rf3
 10 1.0 2.0 3.0
";

            var tables = CalculixDatParser.ParseText(datText);
            var found = CalculixDatExtractors.TryGetNodalReactions(tables, out var reactions);

            Assert.True(found);
            Assert.Single(reactions);
            Assert.Equal(10, reactions[0].NodeId);
            Assert.Equal(1.0, reactions[0].Fx, 12);
            Assert.Equal(3.0, reactions[0].Fz, 12);
        }

        [Fact]
        public void TryGetElementStress_ShouldReturnStressComponents()
        {
            const string datText = @"
 stresses for set e_all
 elem sxx syy szz sxy syz szx
 101 10.0 20.0 30.0 1.0 2.0 3.0
 102 11.0 21.0 31.0 1.1 2.1 3.1
";

            var tables = CalculixDatParser.ParseText(datText);
            var found = CalculixDatExtractors.TryGetElementStress(tables, out var stresses);

            Assert.True(found);
            Assert.Equal(2, stresses.Count);
            Assert.Equal(101, stresses[0].ElementId);
            Assert.Equal(6, stresses[0].Components.Count);
            Assert.Equal(30.0, stresses[0].Components[2], 12);
        }
    }
}
