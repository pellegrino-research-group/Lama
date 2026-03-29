using System;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper;
using Lama.UI;

namespace Lama.Gh
{
	public class LamaInfo : GH_AssemblyInfo
	{
		public override string Name => "Lama";
		public override Bitmap Icon => LoadIcon();
		public override string Description => "Lama Grasshopper plugin";
		public override Guid Id => new Guid("c8a9b2f8-1b2f-4c5a-8a45-6c0a4b0b8c12");
		public override string AuthorName => "Marco Pellegrino";
		public override string AuthorContact => "lama.calculix@gmail.com";

		private static Bitmap LoadIcon()
		{
			return IconLoader.LoadDefaultIcon();
		}
	}

	public class LamaCategoryIcon : GH_AssemblyPriority
	{
		public override GH_LoadingInstruction PriorityLoad()
		{
			Instances.CanvasCreated += MenuLoad.OnStartup;

			Bitmap icon = LoadCategoryIcon();
			if (icon != null)
			{
				Instances.ComponentServer.AddCategoryIcon("Lama", icon);
			}
			Instances.ComponentServer.AddCategorySymbolName("Lama", '\u03BB');
			return GH_LoadingInstruction.Proceed;
		}

		private static Bitmap LoadCategoryIcon()
		{
			return IconLoader.LoadDefaultIcon();
		}
	}
}
