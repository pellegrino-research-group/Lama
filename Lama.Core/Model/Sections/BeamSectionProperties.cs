using System;

namespace Lama.Core.Model.Sections
{
    /// <summary>
    /// Generic beam section properties (single source of truth for A, Iy, Iz, J).
    /// </summary>
    public sealed class BeamSectionProperties
    {
        public double Area { get; }
        public double Iy { get; }
        public double Iz { get; }
        public double J { get; }

        public BeamSectionProperties(double area, double iy, double iz, double j)
        {
            if (area <= 0) throw new ArgumentOutOfRangeException(nameof(area), "Area must be positive.");
            if (iy <= 0) throw new ArgumentOutOfRangeException(nameof(iy), "Iy must be positive.");
            if (iz <= 0) throw new ArgumentOutOfRangeException(nameof(iz), "Iz must be positive.");
            if (j <= 0) throw new ArgumentOutOfRangeException(nameof(j), "J must be positive.");

            Area = area;
            Iy = iy;
            Iz = iz;
            J = j;
        }
    }
}
