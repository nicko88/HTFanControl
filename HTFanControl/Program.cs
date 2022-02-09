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
        static void Main(string[] args)
        {
            if (Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location)).Length > 1)
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
                ConfigHelper.SetupWin();
#if (RELEASE || DEBUG) 
                Task.Factory.StartNew(() => new FanTrayIcon.TrayIcon(ConfigHelper.GetIP()));
#endif
            }
            else
            {
                ConfigHelper.SetupLinux();
            }

            Console.WriteLine("http://" + ConfigHelper.GetIP() + ":5500");

            _ = new Main.WebUI();
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