using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace Aslan.ModelTool.Editor
{
    internal static class ExternalToolRunner
    {
        internal struct ToolRunResult
        {
            public int ExitCode;
            public string StdOut;
            public string StdErr;
            public bool Success;
        }

        public static ToolRunResult Run(string exePath, string arguments, string workingDirectory, int timeoutMs = 600000)
        {
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                return new ToolRunResult
                {
                    ExitCode = -1,
                    StdOut = string.Empty,
                    StdErr = "Executable not found: " + exePath,
                    Success = false
                };
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments ?? string.Empty,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? Environment.CurrentDirectory : workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                return new ToolRunResult
                {
                    ExitCode = -2,
                    StdOut = string.Empty,
                    StdErr = ex.Message,
                    Success = false
                };
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();

            if (!process.WaitForExit(timeoutMs))
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                    // Intentionally ignored.
                }

                return new ToolRunResult
                {
                    ExitCode = -3,
                    StdOut = stdout,
                    StdErr = "Timeout",
                    Success = false
                };
            }

            var result = new ToolRunResult
            {
                ExitCode = process.ExitCode,
                StdOut = stdout,
                StdErr = stderr,
                Success = process.ExitCode == 0
            };

            if (!string.IsNullOrWhiteSpace(result.StdOut))
            {
                UnityEngine.Debug.Log(result.StdOut);
            }

            if (!string.IsNullOrWhiteSpace(result.StdErr))
            {
                UnityEngine.Debug.LogWarning(result.StdErr);
            }

            return result;
        }
    }
}


