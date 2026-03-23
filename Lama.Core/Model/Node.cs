using System;

namespace Lama.Core.Model
{
    /// <summary>
    /// Structural node for FE analysis.
    /// </summary>
    public sealed class Node
    {
        public int Id { get; }
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public Node(int id, double x, double y, double z)
        {
            if (id <= 0)
                throw new ArgumentOutOfRangeException(nameof(id), "Node id must be positive.");

            Id = id;
            X = x;
            Y = y;
            Z = z;
        }
    }
}
