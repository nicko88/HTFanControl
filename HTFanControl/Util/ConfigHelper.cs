using System.Diagnostics;
using System.IO;

namespace HTFanControl.Util
{
    public static class ConfigHelper
    {
        public static readonly string _rootPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);

        public static string OS
        {
            get
            {
                string OS = System.Runtime.InteropServices.RuntimeInformation.OSDescription;

                if (OS.Contains("Windows"))
                {
                    OS = "win";
                }
                else if (OS.Contains("raspi"))
                {
                    OS = "raspi";
                }
                else
                {
                    OS = "linux";
                }

                return OS;
            }
        }

        public static void SetupWin(string port, string instanceName)
        {
            string adminCMD = null;
            string firewall = RunCmd("netsh", $"advfirewall firewall show rule name={instanceName}", false);
            if (!firewall.Contains(instanceName))
            {
                adminCMD = $"netsh advfirewall firewall add rule name=\"{instanceName}\" protocol=TCP dir=in localport={port} action=allow";
            }

            string urlacl = RunCmd("netsh", $"http show urlacl url=http://*:{port}/", false);
            if (!urlacl.Contains($"http://*:{port}/"))
            {
                if (adminCMD != null)
                {
                    adminCMD += " && ";
                }
                adminCMD += $"netsh http add urlacl url=http://*:{port}/ user=%computername%\\%username%";
            }

            if (adminCMD != null)
            {
                RunCmd("cmd", "/C " + adminCMD, true);
            }

            WinRegistry.FixMSEdge();
            WinRegistry.SetMPCHCTimerInterval();
        }

        public static void SetupLinux()
        {
            $"chmod -R 7777 {_rootPath}".Bash();
        }

        public static string RunCmd(string filename, string arguments, bool admin)
        {
            Process process = new Process();
            process.StartInfo.FileName = filename;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.CreateNoWindow = true;

            if (admin)
            {
                process.StartInfo.Verb = "runas";
                process.StartInfo.UseShellExecute = true;
            }
            else
            {
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
            }

            string output = null;
            try
            {
                process.Start();
                if (!admin)
                {
                    output = process.StandardOutput.ReadToEnd();
                }
                process.WaitForExit();
            }
            catch { }

            return output;
        }
    }

    public static class ShellHelper
    {
        public static string Bash(this string cmd)
        {
            string escapedArgs = cmd.Replace("\"", "\\\"");

            Process process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{escapedArgs}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return result;
        }
    }
}