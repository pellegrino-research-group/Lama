using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Resources;

namespace Lama.Gh
{
    internal static class IconLoader
    {
        internal const string ResourceBaseName = "Lama.Properties.Resources";
        internal const string DefaultIconKey = "Lama_24x24";
        internal const string DefaultIconResource = "Lama.Resources.Lama_24x24.png";

        public static Bitmap Load(string key, string fallbackResourceName = null)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                try
                {
                    var resourceManager = new ResourceManager(ResourceBaseName, Assembly.GetExecutingAssembly());
                    if (resourceManager.GetObject(key) is Bitmap bitmap)
                        return CloneToArgb(bitmap);
                }
                catch
                {
                    // fallback to manifest resource
                }
            }

            return LoadFromAssemblyResource(fallbackResourceName ?? DefaultIconResource);
        }

        public static Bitmap LoadDefaultIcon() => Load(DefaultIconKey, DefaultIconResource);

        public static Bitmap LoadFromAssemblyResource(string resourceName)
        {
            if (string.IsNullOrWhiteSpace(resourceName))
                return null;

            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    return null;

                using (var source = new Bitmap(stream, false))
                    return CloneToArgb(source);
            }
        }

        private static Bitmap CloneToArgb(Bitmap source)
        {
            // Clone to a stable 32bpp ARGB bitmap detached from stream/palette.
            var icon = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(icon))
                g.DrawImage(source, 0, 0, source.Width, source.Height);

            return icon;
        }
    }
}
