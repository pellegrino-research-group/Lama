using System.Drawing;

namespace Lama.Gh.Properties
{
    // Lightweight strongly-typed resource access for component icons.
    internal static class Resources
    {
        private static Bitmap _lama24x24;
        private static Bitmap _lama;

        internal static Bitmap Lama_24x24 =>
            _lama24x24 ?? (_lama24x24 = IconLoader.Load("Lama_24x24"));

        internal static Bitmap Lama =>
            _lama ?? (_lama = IconLoader.Load("Lama"));
    }
}
