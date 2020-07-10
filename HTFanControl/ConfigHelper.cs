using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace HTFanControl
{
    public static class ConfigHelper
    {

        public static string GetIP()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return "error";
        }

        public static string GetOS()
        {
            string OS = System.Runtime.InteropServices.RuntimeInformation.OSDescription;

            if(OS.Contains("Windows"))
            {
                OS = "win";
            }
            else if(OS.Contains("raspi"))
            {
                OS = "pi";
            }
            else if (OS.Contains("Linux"))
            {
                OS = "linux";
            }

            return OS;
        }

        public static void SetupWin()
        {
            string adminCMD = null;
            string firewall = RunCmd("netsh", "advfirewall firewall show rule name=HTFanControl", false);
            if (!firewall.Contains("HTFanControl"))
            {
                adminCMD = "netsh advfirewall firewall add rule name=\"HTFanControl\" protocol=TCP dir=in localport=5500 action=allow";
            }

            string urlacl = RunCmd("netsh", "http show urlacl url=http://*:5500/", false);
            if (!urlacl.Contains("http://*:5500/"))
            {
                if (adminCMD != null)
                {
                    adminCMD += " && ";
                }
                adminCMD += "netsh http add urlacl url=http://*:5500/ user=" + Environment.UserName;
            }

            if (adminCMD != null)
            {
                RunCmd("cmd", "/C " + adminCMD, true);
            }

            WinRegistry.FixMSEdge();
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
}