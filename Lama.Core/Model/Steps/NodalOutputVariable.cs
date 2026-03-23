namespace Lama.Core.Model.Steps
{
    /// <summary>
    /// Nodal result variables for CalculiX structural output cards.
    /// Use with <see cref="StepOutputRequest.NodeFile"/> and <see cref="StepOutputRequest.NodePrint"/>.
    /// </summary>
    public enum NodalOutputVariable
    {
        /// <summary>Displacements.</summary>
        U,

        /// <summary>Velocities (dynamic procedures).</summary>
        V,

        /// <summary>Accelerations (dynamic procedures).</summary>
        A,

        /// <summary>Reaction forces/moments at constrained DOFs.</summary>
        RF,

        /// <summary>Concentrated nodal loads.</summary>
        CF
    }
}
