using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HTFanControl
{
    class TrayIcon
    {
        private NotifyIcon trayIcon;
        private ToolStripMenuItem itemConsole;
        private ToolStripMenuItem itemAutostart;

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        public TrayIcon()
        {
            IntPtr handle = GetConsoleWindow();
            ShowWindow(handle, SW_HIDE);

            trayIcon = new NotifyIcon();
            trayIcon.Text = "HTFanControl";
            trayIcon.Icon = new Icon(GetType(), "htfancontrol.ico");
            trayIcon.Click += new EventHandler(trayIcon_Click);

            ToolStripMenuItem itemWebUI = new ToolStripMenuItem();
            itemWebUI.Text = "Open Web UI";
            itemWebUI.Click += new EventHandler(itemWebUI_Click);

            itemConsole = new ToolStripMenuItem();
            itemConsole.Text = "Show Console Window";
            itemConsole.CheckOnClick = true;
            itemConsole.Click += new EventHandler(itemConsole_Click);

            itemAutostart = new ToolStripMenuItem();
            itemAutostart.Text = "Start HTFanControl Automatically";
            itemAutostart.CheckOnClick = true;
            itemAutostart.CheckedChanged += new EventHandler(itemAutostart_CheckedChanged);

            if (CheckRegKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", "HTFanControl"))
            {
                itemAutostart.Checked = true;
            }

            ToolStripSeparator sep1 = new ToolStripSeparator();

            ToolStripMenuItem itemClose = new ToolStripMenuItem();
            itemClose.Text = "Shutdown HTFanControl";
            itemClose.Click += new EventHandler(itemClose_Click);

            ContextMenuStrip trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add(itemWebUI);
            trayMenu.Items.Add(itemConsole);
            trayMenu.Items.Add(itemAutostart);
            trayMenu.Items.Add(sep1);
            trayMenu.Items.Add(itemClose);

            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.Visible = true;

            Application.Run();
        }

        private void trayIcon_Click(object sender, EventArgs e)
        {
            Point position = Cursor.Position;
            position.X -= 253;

            trayIcon.ContextMenuStrip.Show(position);
        }

        private void itemWebUI_Click(object sender, EventArgs e)
        {
            string url = "http://" + ConfigHelper.GetIP() + ":5500";

            try
            {
                Process.Start(url);
            }
            catch
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else
                {
                    throw;
                }
            }
        }

        private void itemConsole_Click(object sender, EventArgs e)
        {
            IntPtr handle = GetConsoleWindow();

            if (itemConsole.Checked)
            {
                ShowWindow(handle, SW_SHOW);
            }
            else
            {
                ShowWindow(handle, SW_HIDE);
            }
        }

        private void itemAutostart_CheckedChanged(object sender, EventArgs e)
        {
            if(itemAutostart.Checked)
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                key.SetValue("HTFanControl", Process.GetCurrentProcess().MainModule.FileName);
            }
            else
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                key.DeleteValue("HTFanControl", false);
            }
        }

        private void itemClose_Click(object sender, EventArgs e)
        {
            trayIcon.Dispose();
            Environment.Exit(0);
        }

        public static bool CheckRegKey(string path, string key)
        {
            try
            {
                RegistryKey regKey = Registry.CurrentUser.OpenSubKey(path, true);
                return (regKey.GetValueNames().Contains(key));
            }
            catch
            {
                return false;
            }
        }
    }
}