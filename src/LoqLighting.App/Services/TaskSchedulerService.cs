using System.Diagnostics;
using System.IO;

namespace LoqLighting.App.Services;

public class TaskSchedulerService
{
    private const string TaskName = "LoqLightingStudio";

    public bool IsTaskScheduled()
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks", $"/query /tn \"{TaskName}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            process?.WaitForExit(3000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public bool SetStartWithWindows(bool enable, bool autoStartProfile = false)
    {
        try
        {
            if (enable)
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName
                    ?? Environment.ProcessPath
                    ?? "";

                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                {
                    return false;
                }

                string args = autoStartProfile ? "--autostart" : "--minimized";
                string command = $"\\\"{exePath}\\\" {args}";

                var psi = new ProcessStartInfo("schtasks", $"/create /tn \"{TaskName}\" /tr \"{command}\" /sc onlogon /rl HIGHEST /f")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                process?.WaitForExit(5000);
                return process?.ExitCode == 0;
            }
            else
            {
                var psi = new ProcessStartInfo("schtasks", $"/delete /tn \"{TaskName}\" /f")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                process?.WaitForExit(5000);
                return true;
            }
        }
        catch
        {
            return false;
        }
    }
}
