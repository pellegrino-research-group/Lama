using Lama.Core.Materials;

namespace Lama.Core.Model.Sections
{
    /// <summary>
    /// Material assignment for solid element sets.
    /// </summary>
    public sealed class SolidSection : SectionBase
    {
        public SolidSection(string elementSetName, MaterialBase material)
            : base(elementSetName, material)
        {
        }
    }
}
