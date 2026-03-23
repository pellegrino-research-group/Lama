using System.Collections.Generic;

namespace Lama.Core.Model.Elements
{
    /// <summary>
    /// Common element contract for model and export layers.
    /// </summary>
    public interface IElement
    {
        int Id { get; }
        string ElementSetName { get; }
        CalculixElementType ElementType { get; }
        IReadOnlyList<int> NodeIds { get; }
    }
}
