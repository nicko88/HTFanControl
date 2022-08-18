using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using HTFanControl.Util;

namespace HTFanControl
{
    class Program
    {
        private static string port = "5500";
        private static string instanceName = "HTFanControl";

        static void Main(string[] args)
        {
            try
            {
                if (!string.IsNullOrEmpty(args[0]))
                {
                    port = args[0];
                }
                if (!string.IsNullOrEmpty(args[1]))
                {
                    instanceName = args[1];
                }
            }
            catch { }


            if (Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location)).Length > 1 && port == "5500")
            {
                Process.GetCurrentProcess().Kill();
            }

            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

            if (!Directory.Exists(Path.Combine(ConfigHelper._rootPath, "windtracks")))
            {
                Directory.CreateDirectory(Path.Combine(ConfigHelper._rootPath, "windtracks"));
            }

            if (!Directory.Exists(Path.Combine(ConfigHelper._rootPath, "tmp")))
            {
                Directory.CreateDirectory(Path.Combine(ConfigHelper._rootPath, "tmp"));
            }

            if (ConfigHelper.GetOS() == "win")
            {
                ConfigHelper.SetupWin(port, instanceName);
#if (RELEASE || DEBUG) 
                Task.Factory.StartNew(() => new FanTrayIcon.TrayIcon(ConfigHelper.GetIP(), port, instanceName));
#endif
            }
            else
            {
                ConfigHelper.SetupLinux();
            }

            Console.WriteLine($"http://{ConfigHelper.GetIP()}:{port}");

            _ = new Main.WebUI(port, instanceName);
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;

            if (!Directory.Exists(Path.Combine(ConfigHelper._rootPath, "crashlogs")))
            {
                Directory.CreateDirectory(Path.Combine(ConfigHelper._rootPath, "crashlogs"));
            }

            string crash = ex.Message + "\n\n" + ex.InnerException + "\n\n" + ex.Source + "\n\n" + ex.StackTrace;
            File.WriteAllText(Path.Combine(ConfigHelper._rootPath, "crashlogs", DateTime.Now.ToString("MM.dd.yy-hh.mm-tt") + ".txt"), crash);
        }
    }
}