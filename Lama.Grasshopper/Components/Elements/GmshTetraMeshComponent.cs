using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Lama.Core.Materials;
using Lama.Core.Meshing;
using Lama.Core.Model.Sections;
using Rhino.Geometry;

namespace Lama.Grasshopper.Components
{
    /// <summary>
    /// Grasshopper component that tetrahedralizes a Brep or Mesh using Gmsh.
    /// For Brep/Mesh inputs the geometry is exported as STL.
    /// Alternatively, a path to a STEP/IGES file can be provided for higher-fidelity
    /// CAD-based meshing via the OpenCASCADE kernel.
    /// The output <c>StructuralModel</c> plugs directly into the CcxModel assembler.
    /// </summary>
    public class GmshTetraMeshComponent : GH_Component
    {
        public GmshTetraMeshComponent()
            : base(
                "Gmsh Tetra Mesh",
                "GmshTet",
                "Generate a tetrahedral volume mesh from a Brep, Mesh, or CAD file (STEP/IGES) " +
                "using the Gmsh mesher. Output is a StructuralModel that can be fed into CcxModel.",
                "Lama",
                "Elements")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Brep", "B", "Brep solid to tetrahedralize.", GH_ParamAccess.item);
            pManager[0].Optional = true;

            pManager.AddMeshParameter("Mesh", "M", "Surface mesh to tetrahedralize.", GH_ParamAccess.item);
            pManager[1].Optional = true;

            pManager.AddTextParameter("CAD File", "F",
                "Optional path to a STEP (.step/.stp), IGES (.iges/.igs), or STL file. " +
                "When provided, Brep/Mesh inputs are ignored.",
                GH_ParamAccess.item);
            pManager[2].Optional = true;

            pManager.AddNumberParameter("Min Size", "Lmin",
                "Minimum characteristic element length.", GH_ParamAccess.item, 1.0);

            pManager.AddNumberParameter("Max Size", "Lmax",
                "Maximum characteristic element length.", GH_ParamAccess.item, 5.0);

            pManager.AddIntegerParameter("Element Order", "Ord",
                "1 = linear tetra (C3D4), 2 = quadratic tetra (C3D10).",
                GH_ParamAccess.item, 2);

            pManager.AddTextParameter("Element Set", "Elset",
                "Element set name for CalculiX.", GH_ParamAccess.item, "E_TET");

            pManager.AddGenericParameter("Material", "Mat",
                "Optional material (MaterialBase) used to auto-create a SolidSection on the output model.",
                GH_ParamAccess.item);
            pManager[7].Optional = true;

            pManager.AddTextParameter("Gmsh Path", "Gmsh",
                "Path to the Gmsh executable. Leave empty to auto-detect.",
                GH_ParamAccess.item);
            pManager[8].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Model", "M", "StructuralModel fragment with tetra elements.", GH_ParamAccess.item);
            pManager.AddMeshParameter("Tetra Meshes", "T", "Tetrahedral meshes (one per element, V:4 F:4).", GH_ParamAccess.list);
            pManager.AddTextParameter("Log", "L", "Gmsh console output (stdout + stderr).", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // --- Collect inputs ---------------------------------------------------
            Brep brep = null;
            Mesh rhinoMesh = null;
            string cadFilePath = null;
            double minSize = 1.0, maxSize = 5.0;
            int order = 2;
            var elementSet = "E_TET";
            object materialObj = null;
            string gmshPath = null;

            DA.GetData(0, ref brep);
            DA.GetData(1, ref rhinoMesh);
            DA.GetData(2, ref cadFilePath);
            DA.GetData(3, ref minSize);
            DA.GetData(4, ref maxSize);
            DA.GetData(5, ref order);
            DA.GetData(6, ref elementSet);
            DA.GetData(7, ref materialObj);
            DA.GetData(8, ref gmshPath);

            // --- Resolve geometry source ------------------------------------------
            string geometryFilePath;
            string workDir;

            if (!string.IsNullOrWhiteSpace(cadFilePath))
            {
                if (!File.Exists(cadFilePath))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                        $"CAD file not found: {cadFilePath}");
                    return;
                }

                geometryFilePath = cadFilePath;
                workDir = Path.GetDirectoryName(cadFilePath);
            }
            else if (brep != null || rhinoMesh != null)
            {
                workDir = Path.Combine(Path.GetTempPath(), "Lama",
                    "Gmsh_" + InstanceGuid.ToString("N").Substring(0, 8));
                Directory.CreateDirectory(workDir);

                geometryFilePath = Path.Combine(workDir, "model.stl");

                try
                {
                    var surfaceMesh = BuildSurfaceMesh(brep, rhinoMesh);
                    WriteAsciiStl(surfaceMesh, geometryFilePath);
                }
                catch (Exception ex)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                        $"Failed to export geometry to STL: {ex.Message}");
                    return;
                }
            }
            else
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Provide a Brep, a Mesh, or a CAD file path.");
                return;
            }

            // --- Resolve material ------------------------------------------------
            MaterialBase material = null;
            if (materialObj != null && !TryUnwrapMaterial(materialObj, out material))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Material input must be a Lama MaterialBase.");
                return;
            }

            // --- Run Gmsh --------------------------------------------------------
            string gmshLog = null;
            try
            {
                var model = GmshTetraMesher.Mesh(
                    geometryFilePath,
                    minSize,
                    maxSize,
                    order,
                    elementSet,
                    gmshPath,
                    out gmshLog);

                model.Name = "GmshTetraFragment";

                if (material != null)
                {
                    model.Materials.Add(material);
                    model.Sections.Add(new SolidSection(elementSet, material));
                }

                var points = model.Nodes
                    .Select(n => new Point3d(n.X, n.Y, n.Z))
                    .ToList();

                var tetraMeshes = BuildTetraMeshes(model);

                DA.SetData(0, model);
                DA.SetDataList(1, tetraMeshes);
                DA.SetData(2, gmshLog);
            }
            catch (Exception ex)
            {
                if (gmshLog != null)
                    DA.SetData(2, gmshLog);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        // ------------------------------------------------------------------
        // Tetra mesh builder
        // ------------------------------------------------------------------

        private static List<Mesh> BuildTetraMeshes(Core.Model.StructuralModel model)
        {
            var nodeMap = new Dictionary<int, Point3d>(model.Nodes.Count);
            foreach (var n in model.Nodes)
                nodeMap[n.Id] = new Point3d(n.X, n.Y, n.Z);

            var meshes = new List<Mesh>(model.Elements.Count);
            foreach (var elem in model.Elements)
            {
                var nids = elem.NodeIds;
                if (nids.Count < 4) continue;

                var m = new Mesh();
                m.Vertices.Add(nodeMap[nids[0]]);
                m.Vertices.Add(nodeMap[nids[1]]);
                m.Vertices.Add(nodeMap[nids[2]]);
                m.Vertices.Add(nodeMap[nids[3]]);

                // 4 triangular faces of the tetrahedron
                m.Faces.AddFace(0, 2, 1);
                m.Faces.AddFace(0, 1, 3);
                m.Faces.AddFace(1, 2, 3);
                m.Faces.AddFace(0, 3, 2);

                m.Normals.ComputeNormals();
                m.Compact();
                meshes.Add(m);
            }

            return meshes;
        }

        // ------------------------------------------------------------------
        // Geometry helpers
        // ------------------------------------------------------------------

        private Mesh BuildSurfaceMesh(Brep brep, Mesh rhinoMesh)
        {
            if (brep != null)
            {
                if (!brep.IsSolid)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "Brep is not a closed solid — volume meshing may fail.");

                var mp = MeshingParameters.Default;
                var pieces = Mesh.CreateFromBrep(brep, mp);
                if (pieces == null || pieces.Length == 0)
                    throw new InvalidOperationException("Rhino failed to mesh the Brep.");

                var joined = new Mesh();
                foreach (var piece in pieces)
                    joined.Append(piece);

                return joined;
            }

            if (rhinoMesh != null)
            {
                if (!rhinoMesh.IsClosed)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "Mesh is not closed — volume meshing may fail.");

                return rhinoMesh.DuplicateMesh();
            }

            throw new InvalidOperationException("No geometry provided.");
        }

        private static void WriteAsciiStl(Mesh mesh, string filePath)
        {
            mesh.FaceNormals.ComputeFaceNormals();

            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("solid model");

                for (var i = 0; i < mesh.Faces.Count; i++)
                {
                    var face = mesh.Faces[i];
                    var normal = mesh.FaceNormals[i];

                    WriteFacet(writer, normal,
                        mesh.Vertices[face.A],
                        mesh.Vertices[face.B],
                        mesh.Vertices[face.C]);

                    if (face.IsQuad)
                    {
                        WriteFacet(writer, normal,
                            mesh.Vertices[face.A],
                            mesh.Vertices[face.C],
                            mesh.Vertices[face.D]);
                    }
                }

                writer.WriteLine("endsolid model");
            }
        }

        private static void WriteFacet(StreamWriter writer, Vector3f normal,
            Point3f v1, Point3f v2, Point3f v3)
        {
            writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "  facet normal {0} {1} {2}", normal.X, normal.Y, normal.Z));
            writer.WriteLine("    outer loop");
            writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "      vertex {0} {1} {2}", v1.X, v1.Y, v1.Z));
            writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "      vertex {0} {1} {2}", v2.X, v2.Y, v2.Z));
            writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "      vertex {0} {1} {2}", v3.X, v3.Y, v3.Z));
            writer.WriteLine("    endloop");
            writer.WriteLine("  endfacet");
        }

        // ------------------------------------------------------------------
        // Material unwrapping
        // ------------------------------------------------------------------

        private static bool TryUnwrapMaterial(object input, out MaterialBase material)
        {
            material = input as MaterialBase;
            if (material != null)
                return true;

            if (input is IGH_Goo goo)
            {
                var scriptValue = goo.ScriptVariable();
                material = scriptValue as MaterialBase;
                if (material != null)
                    return true;
            }

            var valueProp = input.GetType().GetProperty("Value");
            if (valueProp != null)
            {
                var value = valueProp.GetValue(input);
                material = value as MaterialBase;
                if (material != null)
                    return true;
            }

            return false;
        }

        protected override Bitmap Icon => Lama.Grasshopper.Properties.Resources.Lama_24x24;

        public override Guid ComponentGuid => new Guid("7a3e5c12-d8b4-4f91-ae72-c1d3f5e79b08");
    }
}
