using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Lama.Core.Model;

namespace Lama.Core.Meshing
{
    /// <summary>
    /// Generates a Gmsh <c>.geo</c> script, runs the Gmsh executable,
    /// and parses the resulting <c>.msh</c> file into a <see cref="StructuralModel"/>.
    /// Supports STEP/IGES inputs (via OpenCASCADE) and STL inputs (built-in kernel).
    /// </summary>
    public static class GmshTetraMesher
    {
        private static readonly string[] MacPaths =
        {
            "/opt/homebrew/bin/gmsh",
            "/usr/local/bin/gmsh",
            "/Applications/Gmsh.app/Contents/MacOS/gmsh"
        };

        private static readonly string[] WindowsPaths =
        {
            @"C:\Program Files\Gmsh\gmsh.exe",
            @"C:\Program Files (x86)\Gmsh\gmsh.exe"
        };

        /// <summary>
        /// Attempts to locate the Gmsh executable on the system.
        /// </summary>
        public static string FindGmshExecutable()
        {
            string[] paths;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                paths = MacPaths;
            else
                paths = WindowsPaths;

            foreach (var path in paths)
            {
                if (File.Exists(path))
                    return path;
            }

            // Try searching PATH.
            var findCmd = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which";
            var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "gmsh.exe" : "gmsh";

            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = findCmd,
                        Arguments = exeName,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                    return output.Trim().Split('\n')[0].Trim();
            }
            catch
            {
                // Swallow — not found via PATH.
            }

            return null;
        }

        /// <summary>
        /// Runs the full meshing pipeline: .geo generation → Gmsh execution → .msh parsing.
        /// </summary>
        /// <param name="geometryFilePath">
        /// Path to the geometry file (.step, .stp, .iges, .igs, .stl).
        /// The file must already exist on disk.
        /// </param>
        /// <param name="minSize">Minimum characteristic element length.</param>
        /// <param name="maxSize">Maximum characteristic element length.</param>
        /// <param name="elementOrder">1 for linear (C3D4), 2 for quadratic (C3D10).</param>
        /// <param name="elementSetName">CalculiX element set name for the generated elements.</param>
        /// <param name="gmshExecutablePath">
        /// Path to the Gmsh executable. If <c>null</c>, the system is searched automatically.
        /// </param>
        /// <param name="gmshLog">Combined Gmsh stdout + stderr output for diagnostics.</param>
        /// <returns>A <see cref="StructuralModel"/> containing the tetrahedral mesh.</returns>
        public static StructuralModel Mesh(
            string geometryFilePath,
            double minSize,
            double maxSize,
            int elementOrder,
            string elementSetName,
            string gmshExecutablePath,
            out string gmshLog)
        {
            gmshLog = string.Empty;
            if (string.IsNullOrWhiteSpace(geometryFilePath))
                throw new ArgumentException("Geometry file path cannot be empty.", nameof(geometryFilePath));
            if (!File.Exists(geometryFilePath))
                throw new FileNotFoundException("Geometry file not found.", geometryFilePath);
            if (minSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(minSize), "Min size must be positive.");
            if (maxSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxSize), "Max size must be positive.");
            if (maxSize < minSize)
                throw new ArgumentException("Max size must be >= min size.");
            if (elementOrder != 1 && elementOrder != 2)
                throw new ArgumentOutOfRangeException(nameof(elementOrder), "Element order must be 1 or 2.");

            var gmshPath = gmshExecutablePath ?? FindGmshExecutable();
            if (string.IsNullOrWhiteSpace(gmshPath) || !File.Exists(gmshPath))
                throw new FileNotFoundException(
                    "Gmsh executable not found. Install Gmsh or provide the path explicitly.");

            var workDir = Path.GetDirectoryName(geometryFilePath)
                          ?? throw new ArgumentException("Cannot determine working directory from geometry path.");

            var geometryFileName = Path.GetFileName(geometryFilePath);
            var geoFilePath = Path.Combine(workDir, "lama_mesh.geo");
            var mshFilePath = Path.Combine(workDir, "lama_mesh.msh");

            WriteGeoScript(geoFilePath, geometryFileName, minSize, maxSize, elementOrder);

            var (exitCode, stdout, stderr) = RunGmsh(gmshPath, geoFilePath, mshFilePath);

            gmshLog = string.IsNullOrEmpty(stderr)
                ? stdout
                : stdout + "\n--- STDERR ---\n" + stderr;

            if (exitCode != 0)
                throw new InvalidOperationException(
                    $"Gmsh exited with code {exitCode}.\nStdout: {stdout}\nStderr: {stderr}");

            if (!File.Exists(mshFilePath))
                throw new FileNotFoundException(
                    $"Gmsh did not produce the expected output file: {mshFilePath}");

            return GmshMshParser.Parse(mshFilePath, elementSetName);
        }

        /// <summary>
        /// Writes a Gmsh <c>.geo</c> script for the given geometry file.
        /// Uses the OpenCASCADE kernel for STEP/IGES files, and the built-in
        /// kernel with <c>CreateTopology</c> for STL files.
        /// </summary>
        internal static void WriteGeoScript(
            string geoFilePath,
            string geometryFileName,
            double minSize,
            double maxSize,
            int elementOrder)
        {
            var ext = Path.GetExtension(geometryFileName).ToLowerInvariant();
            var isSTL = ext == ".stl";

            using (var writer = new StreamWriter(geoFilePath))
            {
                writer.WriteLine("// Auto-generated by Lama – do not edit.");

                if (isSTL)
                {
                    // STL produces discrete entities \u2013 use built-in kernel.
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "Merge \"{0}\";", geometryFileName));
                    writer.WriteLine("CreateTopology;");
                    writer.WriteLine("Surface Loop(1) = Surface{:};");
                    writer.WriteLine("Volume(1) = {1};");
                }
                else
                {
                    // CAD files (STEP/IGES/BREP) \u2013 use OpenCASCADE kernel.
                    writer.WriteLine("SetFactory(\"OpenCASCADE\");");
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "Merge \"{0}\";", geometryFileName));
                }

                writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "Mesh.CharacteristicLengthMin = {0};", minSize));
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "Mesh.CharacteristicLengthMax = {0};", maxSize));
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "Mesh.ElementOrder = {0};", elementOrder));

                // --- 3D meshing algorithm ---
                // 1=Delaunay, 4=Frontal, 7=MMG3D, 10=HXT
                writer.WriteLine("Mesh.Algorithm3D = 1;");

                // --- Optimization for FEA-quality tetrahedra ---
                writer.WriteLine("Mesh.Optimize = 1;");
                writer.WriteLine("Mesh.OptimizeNetgen = 1;");
                writer.WriteLine("Mesh.OptimizeThreshold = 0.3;");
                writer.WriteLine("Mesh.Smoothing = 5;");

                // High-order optimization (relevant when ElementOrder = 2).
                writer.WriteLine("Mesh.HighOrderOptimize = 2;");

                // Quality bounds — Gmsh will warn if elements fall below these.
                writer.WriteLine("Mesh.QualityType = 2;");     // 2 = SICN (Scaled Inverse Condition Number)
                writer.WriteLine("Mesh.AnisoMax = 1e10;");
            }
        }

        private static (int ExitCode, string Stdout, string Stderr) RunGmsh(
            string gmshPath,
            string geoFilePath,
            string mshFilePath)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = gmshPath,
                    Arguments = string.Format(
                        CultureInfo.InvariantCulture,
                        "\"{0}\" -3 -format msh2 -o \"{1}\"",
                        geoFilePath, mshFilePath),
                    WorkingDirectory = Path.GetDirectoryName(geoFilePath),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();

            var outTask = Task.Run(() => process.StandardOutput.ReadToEnd());
            var errTask = Task.Run(() => process.StandardError.ReadToEnd());
            process.WaitForExit();
            Task.WaitAll(outTask, errTask);

            return (process.ExitCode, outTask.Result, errTask.Result);
        }
    }
}
