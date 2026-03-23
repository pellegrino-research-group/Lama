using System;

namespace Lama.Core.Materials
{
    /// <summary>
    /// One point in an isotropic hardening curve.
    /// </summary>
    public sealed class PlasticPoint
    {
        public double YieldStress { get; set; }
        public double EquivalentPlasticStrain { get; set; }

        public PlasticPoint(double yieldStress, double equivalentPlasticStrain)
        {
            YieldStress = yieldStress;
            EquivalentPlasticStrain = equivalentPlasticStrain;
        }
    }
}
