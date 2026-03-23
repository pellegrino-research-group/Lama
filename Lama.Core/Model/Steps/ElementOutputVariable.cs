namespace Lama.Core.Model.Steps
{
    /// <summary>
    /// Element or integration-point result variables for CalculiX output cards.
    /// Use with <see cref="StepOutputRequest.ElementFile"/> and <see cref="StepOutputRequest.ElementPrint"/>.
    /// </summary>
    public enum ElementOutputVariable
    {
        /// <summary>Stress tensor components.</summary>
        S,

        /// <summary>Total strain tensor components.</summary>
        E,

        /// <summary>Plastic strain components.</summary>
        PE,

        /// <summary>Equivalent plastic strain.</summary>
        PEEQ,

        /// <summary>Strain energy density / energy-related output.</summary>
        ENER,

        /// <summary>User/internal state variables.</summary>
        SDV
    }
}
