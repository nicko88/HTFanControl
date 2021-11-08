using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace HTFanControl
{
    class Program
    {
        private static string _rootDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
        static void Main(string[] args)
        {
            if (Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location)).Length > 1)
            {
                Process.GetCurrentProcess().Kill();
            }

            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

            if (ConfigHelper.GetOS() == "win")
            {
                ConfigHelper.SetupWin();
#if (RELEASE || DEBUG) 
                Task.Factory.StartNew(() => new FanTrayIcon.TrayIcon(ConfigHelper.GetIP()));
#endif
            }

            if (!Directory.Exists(Path.Combine(_rootDir, "windtracks")))
            {
                Directory.CreateDirectory(Path.Combine(_rootDir, "windtracks"));
            }

            if (!Directory.Exists(Path.Combine(_rootDir, "tmp")))
            {
                Directory.CreateDirectory(Path.Combine(_rootDir, "tmp"));
            }

            Console.WriteLine("http://" + ConfigHelper.GetIP() + ":5500");

            _ = new WebUI();
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;

            if (!Directory.Exists(Path.Combine(_rootDir, "crashlogs")))
            {
                Directory.CreateDirectory(Path.Combine(_rootDir, "crashlogs"));
            }

            string crash = ex.Message + "\n\n" + ex.InnerException + "\n\n" + ex.Source + "\n\n" + ex.StackTrace;
            File.WriteAllText(Path.Combine(_rootDir, "crashlogs", DateTime.Now.ToString("MM.dd.yy-hh.mm-tt") + ".txt"), crash);
        }
    }
}