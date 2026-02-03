using System;

namespace Lama.Core.Materials
{
    /// <summary>
    /// Isotropic material with uniform properties in all directions
    /// </summary>
    public class IsotropicMaterial : MaterialBase
    {
        public override string MaterialType => "Isotropic";

        public double YoungModulus { get; set; }
        public double PoissonRatio { get; set; }

        public IsotropicMaterial(string name) : base(name)
        {
        }

        public override string ToString()
        {
            return $"Isotropic Material: {Name}";
        }
    }
}

