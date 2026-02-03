using System;
using System.Drawing;

namespace Lama.Core.Materials
{
    /// <summary>
    /// Base class for all material definitions
    /// </summary>
    public abstract class MaterialBase
    {
        public string Name { get; set; }
        public Color Color { get; set; }
        public double Density { get; set; }
        public abstract string MaterialType { get; }

        protected MaterialBase(string name)
        {
            Name = name;
        }

        public override string ToString()
        {
            return $"{MaterialType}: {Name}";
        }
    }
}

