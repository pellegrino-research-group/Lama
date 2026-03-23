using System;
using Lama.Core.Materials;

namespace Lama.Core.Model.Sections
{
    /// <summary>
    /// Base section assignment for an element set.
    /// </summary>
    public abstract class SectionBase
    {
        public string ElementSetName { get; }
        public MaterialBase Material { get; }

        protected SectionBase(string elementSetName, MaterialBase material)
        {
            if (string.IsNullOrWhiteSpace(elementSetName))
                throw new ArgumentException("Element set name cannot be empty.", nameof(elementSetName));

            ElementSetName = elementSetName;
            Material = material ?? throw new ArgumentNullException(nameof(material));
        }
    }
}
