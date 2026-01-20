using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace RailStudio.Services
{
    public interface IBuilderService
    {
        Task RunBuilderAsync(string pythonPath, string scriptPath, string args, Action<string> outputHandler);
    }

    public class BuilderService : IBuilderService
    {
        public async Task RunBuilderAsync(string pythonPath, string scriptPath, string args, Action<string> outputHandler)
        {
            // ENTERPRISE MODE: If pythonPath is empty, scriptPath is actually the .exe
            bool isExecutable = string.IsNullOrWhiteSpace(pythonPath);
            
            if (isExecutable)
            {
                // Direct .exe execution (zero-dependency mode)
                if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath))
                {
                    outputHandler($"Error: Builder executable not found. Path: '{scriptPath}'");
                    return;
                }
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = scriptPath, // .exe directly
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(scriptPath) ?? string.Empty,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };
                
                await ExecuteProcess(startInfo, outputHandler, scriptPath, args);
            }
            else
            {
                // Legacy mode: Python + script (for development)
                // Allow "python" or "python3" to pass without File.Exists check if they are in PATH
                bool isCommand = !pythonPath.Contains(Path.DirectorySeparatorChar) && !pythonPath.Contains(Path.AltDirectorySeparatorChar);
                if (string.IsNullOrWhiteSpace(pythonPath) || (!isCommand && !File.Exists(pythonPath)))
                {
                    outputHandler($"Error: Python interpreter not found or path is empty. Path: '{pythonPath}'");
                    return;
                }

                if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath))
                {
                    outputHandler("Error: Builder script not found or path is empty.");
                    return;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = $"\"{scriptPath}\" {args}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(scriptPath) ?? string.Empty,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };
                
                // Force Python to use UTF-8 for IO
                startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                await ExecuteProcess(startInfo, outputHandler, pythonPath, $"\"{scriptPath}\" {args}");
            }
        }
        
        private async Task ExecuteProcess(ProcessStartInfo startInfo, Action<string> outputHandler, string command, string args)
        {
            using (var process = new Process { StartInfo = startInfo })
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null) outputHandler(e.Data);
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null) outputHandler($"ERROR: {e.Data}");
                };

                outputHandler($"Starting build process...");
                outputHandler($"Command: {command} {args}");

                try
                {
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    await process.WaitForExitAsync();

                    outputHandler($"Process exited with code {process.ExitCode}");
                }
                catch (Exception ex)
                {
                    outputHandler($"Exception starting process: {ex.Message}");
                }
            }
        }
    }
}




