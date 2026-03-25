using Lama.Core.Materials;

namespace Lama.Core.Model.Sections
{
    /// <summary>
    /// Material assignment for solid element sets.
    /// </summary>
    public sealed class SolidSection : SectionBase
    {
        public SectionOrientation Orientation { get; }

        public SolidSection(string elementSetName, MaterialBase material, SectionOrientation orientation = null)
            : base(elementSetName, material)
        {
            Orientation = orientation;
        }
    }
}
