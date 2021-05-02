using System;
using System.Diagnostics;
using System.IO;

namespace HTFanControl
{
    class Program
    {
        static void Main(string[] args)
        {
            if (Process.GetProcessesByName(Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location)).Length > 1)
            {
                Process.GetCurrentProcess().Kill();
            }

            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

            if (ConfigHelper.GetOS() == "win")
            {
                ConfigHelper.SetupWin();
#if (RELEASE || DEBUG) 
                System.Threading.Tasks.Task.Factory.StartNew(() => new FanTrayIcon.TrayIcon(ConfigHelper.GetIP()));
#endif
            }

            if (!Directory.Exists(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "windtracks")))
            {
                Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "windtracks"));
            }

            if (!Directory.Exists(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "fingerprints")))
            {
                Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "fingerprints"));
            }

            Console.WriteLine("http://" + ConfigHelper.GetIP() + ":5500");

            _ = new WebUI();
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;

            if (!Directory.Exists(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "crashlogs")))
            {
                Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "crashlogs"));
            }

            string crash = ex.Message + "\n\n" + ex.InnerException + "\n\n" + ex.Source + "\n\n" + ex.StackTrace;
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "crashlogs", DateTime.Now.ToString("MM.dd.yy-hh.mm-tt") + ".txt"), crash);
        }
    }
}