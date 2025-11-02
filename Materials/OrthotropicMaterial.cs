using System;

namespace Lama.Materials
{
    /// <summary>
    /// Orthotropic material with different properties in three orthogonal directions
    /// </summary>
    public class OrthotropicMaterial : MaterialBase
    {
        public override string MaterialType => "Orthotropic";

        public double E1 { get; set; }
        public double E2 { get; set; }
        public double E3 { get; set; }
        public double Nu12 { get; set; }
        public double Nu13 { get; set; }
        public double Nu23 { get; set; }
        public double G12 { get; set; }
        public double G13 { get; set; }
        public double G23 { get; set; }

        public OrthotropicMaterial(string name) : base(name)
        {
        }

        public override string ToString()
        {
            return $"Orthotropic Material: {Name}";
        }
    }
}

