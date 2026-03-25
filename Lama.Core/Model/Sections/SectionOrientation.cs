using System;

namespace Lama.Core.Model.Sections
{
    /// <summary>
    /// Local material orientation defined by axis-1 and an in-plane reference axis-2.
    /// Axis-2 is orthogonalized against axis-1 and both are normalized.
    /// </summary>
    public sealed class SectionOrientation
    {
        public double Axis1X { get; }
        public double Axis1Y { get; }
        public double Axis1Z { get; }
        public double Axis2X { get; }
        public double Axis2Y { get; }
        public double Axis2Z { get; }

        public SectionOrientation(
            double axis1X, double axis1Y, double axis1Z,
            double axis2X, double axis2Y, double axis2Z)
        {
            var a1Length = Math.Sqrt(axis1X * axis1X + axis1Y * axis1Y + axis1Z * axis1Z);
            if (a1Length < 1e-15)
                throw new ArgumentException("Axis 1 vector must be non-zero.");

            var ux = axis1X / a1Length;
            var uy = axis1Y / a1Length;
            var uz = axis1Z / a1Length;

            // Gram-Schmidt: remove axis-2 component along axis-1.
            var dot = (axis2X * ux) + (axis2Y * uy) + (axis2Z * uz);
            var vx = axis2X - (dot * ux);
            var vy = axis2Y - (dot * uy);
            var vz = axis2Z - (dot * uz);

            var vLength = Math.Sqrt(vx * vx + vy * vy + vz * vz);
            if (vLength < 1e-15)
                throw new ArgumentException("Axis 2 vector must not be parallel to Axis 1.");

            Axis1X = ux;
            Axis1Y = uy;
            Axis1Z = uz;
            Axis2X = vx / vLength;
            Axis2Y = vy / vLength;
            Axis2Z = vz / vLength;
        }
    }
}
