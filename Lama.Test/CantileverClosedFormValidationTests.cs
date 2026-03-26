using System;
using System.IO;
using System.Linq;
using Lama.Core.Application;
using Lama.Core.PostProcessing;
using Xunit;

namespace Lama.Test
{
    public class CantileverClosedFormValidationTests
    {
        [Fact]
        public void BeamTipDisplacement_ShouldMatchEulerBernoulliClosedForm()
        {
            if (!ShouldRunCalculixIntegration())
                return;

            var executablePath = ResolveCalculixExecutable();
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
                return;

            var outputDirectory = CreatePersistentProjectOutputDirectory();
            var jobName = "beam_closed_form";
            var sourceInputPath = Path.Combine(
                FindRepositoryRoot(Directory.GetCurrentDirectory()),
                "Lama.Test",
                "example_1",
                "test_beam.inp");

            var targetInputPath = Path.Combine(outputDirectory, $"{jobName}.inp");
            var inputText = File.ReadAllText(sourceInputPath);
            inputText = inputText.Replace(
                "*END STEP",
                "*NODE PRINT,NSET=LOAD_NODE\nU\n*END STEP",
                StringComparison.Ordinal);
            File.WriteAllText(targetInputPath, inputText);

            var (exitCode, _, _) = CalculixApplication.RunCalculix(executablePath, targetInputPath, outputDirectory, numberOfCores: 1);
            Assert.Equal(0, exitCode);

            var datPath = Path.Combine(outputDirectory, $"{jobName}.dat");
            Assert.True(File.Exists(datPath));

            var tables = CalculixDatParser.ParseFile(datPath);
            Assert.True(CalculixDatExtractors.TryGetNodalDisplacements(tables, out var displacements));

            var tip = displacements.Single(d => d.NodeId == 11);
            var tipUy = Math.Abs(tip.Y);

            // Euler-Bernoulli cantilever with end point load: delta = P*L^3 / (3*E*I)
            const double loadN = 1000.0;
            const double lengthM = 1.0;
            const double youngModulusPa = 210e9;
            const double widthM = 0.1;
            const double heightM = 0.05;
            var secondMomentArea = widthM * Math.Pow(heightM, 3) / 12.0;
            var closedForm = loadN * Math.Pow(lengthM, 3) / (3.0 * youngModulusPa * secondMomentArea);

            var relativeError = Math.Abs(tipUy - closedForm) / closedForm;
            Assert.True(relativeError < 0.03, $"Relative error too high. FE={tipUy:E6}, closed-form={closedForm:E6}, relErr={relativeError:P3}");
        }

        private static bool ShouldRunCalculixIntegration()
        {
            var runFlag = Environment.GetEnvironmentVariable("LAMA_RUN_CALCULIX_INTEGRATION");
            return string.Equals(runFlag, "1", StringComparison.Ordinal);
        }

        private static string ResolveCalculixExecutable()
        {
            var fromEnvironment = Environment.GetEnvironmentVariable("CCX_EXE");
            if (!string.IsNullOrWhiteSpace(fromEnvironment))
                return fromEnvironment;

            return CalculixApplication.FindCalculixExecutable();
        }

        private static string CreatePersistentProjectOutputDirectory()
        {
            var root = FindRepositoryRoot(Directory.GetCurrentDirectory());
            var outputDirectory = Path.Combine(
                root,
                "Lama.Test",
                "generated",
                "beam_closed_form",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));

            Directory.CreateDirectory(outputDirectory);
            return outputDirectory;
        }

        private static string FindRepositoryRoot(string startDirectory)
        {
            var current = new DirectoryInfo(startDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "Lama.sln")))
                    return current.FullName;

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate repository root containing Lama.sln.");
        }
    }
}
