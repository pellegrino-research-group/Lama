using Grasshopper.Kernel;
using System.Drawing;

namespace Lama.Grasshopper.Widgets
{
    public class StandardFont
    {
        public static Font font()
        {
            return GH_FontServer.StandardAdjusted;
        }

        public static Font largeFont()
        {
            return GH_FontServer.LargeAdjusted;
        }
    }
}
