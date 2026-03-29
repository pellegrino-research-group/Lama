using System;
using System.Collections.Generic;
using System.Linq;
using Lama.Core.Materials;
using Lama.Core.Model.Sections;
using Rhino.Geometry;

namespace Lama.Gh.Definitions
{
    /// <summary>
    /// Container for Rhino hexahedral meshes and conversion options.
    /// </summary>
    public sealed class HexMeshDefinition
    {
        public HexMeshDefinition(
            IEnumerable<Mesh> meshes,
            string elementSetName,
            MaterialBase material = null,
            SectionOrientation orientation = null)
        {
            if (meshes == null)
                throw new ArgumentNullException(nameof(meshes));
            if (string.IsNullOrWhiteSpace(elementSetName))
                throw new ArgumentException("Element set name cannot be empty.", nameof(elementSetName));

            var meshList = meshes.Where(m => m != null).Select(m => m.DuplicateMesh()).ToList();
            if (meshList.Count == 0)
                throw new ArgumentException("At least one mesh is required.", nameof(meshes));

            Meshes = meshList;
            ElementSetName = elementSetName;
            Material = material;
            Orientation = orientation;
        }

        public IReadOnlyList<Mesh> Meshes { get; }

        public string ElementSetName { get; }

        public MaterialBase Material { get; }

        public SectionOrientation Orientation { get; }
    }
}
