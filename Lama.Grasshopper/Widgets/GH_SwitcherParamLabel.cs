using System.Drawing;
using Grasshopper.GUI;
using Grasshopper.Kernel;

namespace Lama.Gh.Widgets
{
    /// <summary>
    /// Canvas labels for switcher parameters: show full <see cref="IGH_Param.Name"/>,
    /// with layout width that fits either name or nickname so text is not clipped.
    /// </summary>
    internal static class GH_SwitcherParamLabel
    {
        public static string CanvasText(IGH_Param param) => param.Name;

        public static int MeasureWidth(IGH_Param param, Font font)
        {
            return System.Math.Max(
                GH_FontServer.StringWidth(param.Name, font),
                GH_FontServer.StringWidth(param.NickName, font));
        }
    }
}
