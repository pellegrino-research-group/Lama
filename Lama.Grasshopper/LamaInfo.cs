using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using Grasshopper.Kernel;
using Grasshopper;

namespace Lama.Grasshopper
{
	public class LamaInfo : GH_AssemblyInfo
	{
		public override string Name => "Lama";
		public override Bitmap Icon => LoadIcon();
		public override string Description => "Lama Grasshopper plugin";
		public override Guid Id => new Guid("c8a9b2f8-1b2f-4c5a-8a45-6c0a4b0b8c12");
		public override string AuthorName => "Lama";
		public override string AuthorContact => "";

		private static Bitmap LoadIcon()
		{
			try
			{
				var assembly = Assembly.GetExecutingAssembly();
				var resourceName = "Lama.Lama.Grasshopper.Resources.Lama_24_24.png";
				using (Stream stream = assembly.GetManifestResourceStream(resourceName))
				{
					if (stream != null)
					{
						return new Bitmap(stream);
					}
				}
			}
			catch
			{
				// Return null if icon cannot be loaded
			}
			return null;
		}
	}

	public class LamaCategoryIcon : GH_AssemblyPriority
	{
		public override GH_LoadingInstruction PriorityLoad()
		{
			Bitmap icon = LoadCategoryIcon();
			if (icon != null)
			{
				Instances.ComponentServer.AddCategoryIcon("Lama", icon);
			}
			Instances.ComponentServer.AddCategorySymbolName("Lama", 'L');
			return GH_LoadingInstruction.Proceed;
		}

		private static Bitmap LoadCategoryIcon()
		{
			try
			{
				var assembly = Assembly.GetExecutingAssembly();
				var resourceName = "Lama.Lama.Grasshopper.Resources.Lama_24_24.png";
				using (Stream stream = assembly.GetManifestResourceStream(resourceName))
				{
					if (stream != null)
					{
						return new Bitmap(stream);
					}
				}
			}
			catch
			{
				// Return null if icon cannot be loaded
			}
			return null;
		}
	}
}
