using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Lama.Core.Application
{
    /// <summary>
    /// Application-level functionality for CalculiX integration
    /// </summary>
    public static class CalculixApplication
    {
        /// <summary>
        /// Gets the current operating system platform
        /// </summary>
        public static OSPlatform CurrentPlatform
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return OSPlatform.Windows;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    return OSPlatform.OSX;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return OSPlatform.Linux;
                
                throw new PlatformNotSupportedException("Unsupported operating system");
            }
        }

        /// <summary>
        /// Checks if the current platform is macOS
        /// </summary>
        public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        /// <summary>
        /// Checks if the current platform is Windows
        /// </summary>
        public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        /// <summary>
        /// Checks if the current platform is Linux
        /// </summary>
        public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        /// <summary>
        /// Default CalculiX executable name for the current platform
        /// </summary>
        public static string DefaultExecutableName => IsWindows ? "ccx.exe" : "ccx";

        /// <summary>
        /// Common CalculiX installation paths for macOS
        /// </summary>
        public static readonly string[] MacOSCommonPaths = new[]
        {
            "/usr/local/bin/ccx",
            "/opt/homebrew/bin/ccx",
            "/opt/local/bin/ccx"
        };

        /// <summary>
        /// Common CalculiX installation paths for Windows
        /// </summary>
        public static readonly string[] WindowsCommonPaths = new[]
        {
            @"C:\Program Files\CalculiX\ccx.exe",
            @"C:\CalculiX\ccx.exe",
            @"C:\Program Files (x86)\CalculiX\ccx.exe"
        };

        /// <summary>
        /// Attempts to find the CalculiX executable on the system
        /// </summary>
        /// <returns>Path to CalculiX executable, or null if not found</returns>
        public static string FindCalculixExecutable()
        {
            // Check common paths based on platform
            string[] pathsToCheck = IsMacOS ? MacOSCommonPaths : 
                                    IsWindows ? WindowsCommonPaths : 
                                    new string[0];

            foreach (var path in pathsToCheck)
            {
                if (File.Exists(path))
                    return path;
            }

            // Try to find in PATH using 'which' (Unix) or 'where' (Windows)
            string findCommand = IsWindows ? "where" : "which";
            
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = findCommand,
                        Arguments = DefaultExecutableName,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    return output.Trim().Split('\n')[0].Trim();
                }
            }
            catch
            {
                // Failed to find using system commands
            }

            return null;
        }

        /// <summary>
        /// Validates if a CalculiX executable exists at the specified path
        /// </summary>
        /// <param name="executablePath">Path to the executable</param>
        /// <returns>True if the executable exists and is accessible</returns>
        public static bool ValidateExecutable(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
                return false;

            return File.Exists(executablePath);
        }

        /// <summary>
        /// Runs CalculiX with the specified input file
        /// </summary>
        /// <param name="executablePath">Path to CalculiX executable</param>
        /// <param name="inputFilePath">Path to input file (without extension)</param>
        /// <param name="workingDirectory">Working directory for the process</param>
        /// <returns>Exit code from CalculiX</returns>
        public static int RunCalculix(string executablePath, string inputFilePath, string workingDirectory = null)
        {
            if (!ValidateExecutable(executablePath))
                throw new FileNotFoundException($"CalculiX executable not found at: {executablePath}");

            if (string.IsNullOrWhiteSpace(inputFilePath))
                throw new ArgumentException("Input file path cannot be empty", nameof(inputFilePath));

            // Remove file extension if present (CalculiX expects basename)
            string inputBaseName = Path.GetFileNameWithoutExtension(inputFilePath);
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = $"-i {inputBaseName}",
                    WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(inputFilePath) ?? Directory.GetCurrentDirectory(),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            process.WaitForExit();

            return process.ExitCode;
        }

        /// <summary>
        /// Gets platform-specific information string
        /// </summary>
        public static string GetPlatformInfo()
        {
            return $"Platform: {CurrentPlatform}, Architecture: {RuntimeInformation.ProcessArchitecture}";
        }
    }
}
