using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace Lama
{
	public class LamaInfo : GH_AssemblyInfo
	{
		public override string Name => "Lama";

		public override Bitmap Icon => null;

		public override string Description => "Lama Grasshopper plugin";

		public override Guid Id => new Guid("c8a9b2f8-1b2f-4c5a-8a45-6c0a4b0b8c12");

		public override string AuthorName => "Lama";

		public override string AuthorContact => "";
	}
}
