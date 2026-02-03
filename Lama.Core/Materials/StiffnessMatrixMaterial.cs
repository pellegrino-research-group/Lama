using System;

namespace Lama.Core.Materials
{
    /// <summary>
    /// Material defined by a full stiffness matrix
    /// </summary>
    public class StiffnessMatrixMaterial : MaterialBase
    {
        public override string MaterialType => "Stiffness Matrix";

        // TODO: Add material properties when specified
        // Example properties (to be defined by user):
        // public double[,] StiffnessMatrix { get; set; } // 6x6 stiffness matrix
        // public double Density { get; set; }

        public StiffnessMatrixMaterial(string name) : base(name)
        {
        }

        public override string ToString()
        {
            return $"Stiffness Matrix Material: {Name}";
        }
    }
}

