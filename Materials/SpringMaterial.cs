using System;

namespace Lama.Materials
{
    /// <summary>
    /// Spring material for connector/spring elements
    /// </summary>
    public class SpringMaterial : MaterialBase
    {
        public override string MaterialType => "Spring";
        public double SpringConstant { get; set; }

        public SpringMaterial(string name) : base(name)
        {
        }

        public override string ToString()
        {
            return $"Spring Material: {Name}";
        }
    }
}

