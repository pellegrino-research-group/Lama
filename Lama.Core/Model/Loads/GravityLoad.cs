using System;

namespace Lama.Core.Model.Loads
{
    /// <summary>
    /// Gravity body load applied via *DLOAD with the GRAV type.
    /// The direction vector is normalised on construction.
    /// </summary>
    public sealed class GravityLoad
    {
        public double Magnitude { get; }
        public double DirectionX { get; }
        public double DirectionY { get; }
        public double DirectionZ { get; }

        /// <summary>
        /// Target element set. Empty means all elements (EALL).
        /// </summary>
        public string ElementSetName { get; }

        public GravityLoad(double magnitude, double directionX, double directionY, double directionZ, string elementSetName = null)
        {
            if (magnitude < 0)
                throw new ArgumentOutOfRangeException(nameof(magnitude), "Gravity magnitude must be non-negative.");

            var length = Math.Sqrt(directionX * directionX + directionY * directionY + directionZ * directionZ);
            if (length < 1e-15)
                throw new ArgumentException("Direction vector must be non-zero.");

            Magnitude = magnitude;
            DirectionX = directionX / length;
            DirectionY = directionY / length;
            DirectionZ = directionZ / length;
            ElementSetName = elementSetName ?? string.Empty;
        }
    }
}
