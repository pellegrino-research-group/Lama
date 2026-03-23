using Lama.Core.PostProcessing;
using Xunit;

namespace Lama.Test
{
    public class CalculixDatParserTests
    {
        [Fact]
        public void ParseText_ShouldExtractNodalAndElementNumericTables()
        {
            const string datText = @"
 displacements for set all
 node  u1         u2         u3
 1     0.0        0.0       -1.2E-03
 2     1.0E-04    0.0       -2.3E-03

 stresses for set e_all
 elem  sxx        syy        szz
 10    1.00E+02   2.00E+02   3.00E+02
 11    1.10E+02   2.10E+02   3.10E+02
";

            var tables = CalculixDatParser.ParseText(datText);

            Assert.Equal(2, tables.Count);
            Assert.Equal(2, tables[0].Rows.Count);
            Assert.Equal(2, tables[1].Rows.Count);
            Assert.Equal(1, tables[0].Rows[0].EntityId);
            Assert.Equal(-1.2E-03, tables[0].Rows[0].Values[2], 12);
        }

        [Fact]
        public void FindTablesByHeaderKeyword_ShouldFilterMatchingTables()
        {
            const string datText = @"
 displacements for set all
 1 0.0 0.0 0.0

 stresses for set e_all
 10 1.0 2.0 3.0
";

            var tables = CalculixDatParser.ParseText(datText);
            var stressTables = CalculixDatParser.FindTablesByHeaderKeyword(tables, "stress");

            Assert.Single(stressTables);
            Assert.Single(stressTables[0].Rows);
            Assert.Equal(10, stressTables[0].Rows[0].EntityId);
        }
    }
}
