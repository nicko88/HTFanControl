using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FanTrayIcon
{
    public class TrayIcon
    {
        string _port;
        string _instanceName;

        private NotifyIcon trayIcon;
        private ToolStripMenuItem itemConsole;
        private ToolStripMenuItem itemAutostart;

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        private static string IP
        {
            get
            {
                string IP = "IPError";
                foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if ((item.NetworkInterfaceType == NetworkInterfaceType.Ethernet || item.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) &&
                        !item.Description.ToLower().Contains("virtual") && !item.Name.ToLower().Contains("virtual") && item.OperationalStatus == OperationalStatus.Up)
                    {
                        foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == AddressFamily.InterNetwork && !ip.Address.ToString().StartsWith("127"))
                            {
                                IP = ip.Address.ToString();
                            }
                        }
                    }
                }
                return IP;
            }
        }

        public TrayIcon(string port, string instanceName)
        {
            _port = port;
            _instanceName = instanceName;

            IntPtr handle = GetConsoleWindow();
            ShowWindow(handle, SW_HIDE);

            trayIcon = new NotifyIcon();
            trayIcon.Text = $"{_instanceName} (Right Click For Menu)";
            trayIcon.Icon = new Icon(GetType(), "htfancontrol.ico");
            trayIcon.MouseClick += new MouseEventHandler(trayIcon_MouseClick);
            trayIcon.DoubleClick += new EventHandler(trayIcon_DoubleClick);

            ToolStripMenuItem itemWebUI = new ToolStripMenuItem();
            itemWebUI.Text = "Open Web UI";
            itemWebUI.Click += new EventHandler(itemWebUI_Click);

            itemConsole = new ToolStripMenuItem();
            itemConsole.Text = "Show Console Window (Log)";
            itemConsole.CheckOnClick = true;
            itemConsole.Click += new EventHandler(itemConsole_Click);

            itemAutostart = new ToolStripMenuItem();
            itemAutostart.Text = $"Start {_instanceName} Automatically";
            itemAutostart.CheckOnClick = true;
            itemAutostart.CheckedChanged += new EventHandler(itemAutostart_CheckedChanged);

            if (CheckRegKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", _instanceName))
            {
                itemAutostart.Checked = true;
            }

            ToolStripSeparator sep1 = new ToolStripSeparator();

            ToolStripMenuItem itemClose = new ToolStripMenuItem();
            itemClose.Text = $"Shutdown {_instanceName}";
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

        private void trayIcon_MouseClick(object sender, MouseEventArgs e)
        {
            Point position = Cursor.Position;
            position.X -= 253;
            position.Y -= 100;

            if (e.Button == MouseButtons.Right)
            {
                trayIcon.ContextMenuStrip.Show(position);
            }
        }

        private void trayIcon_DoubleClick(object sender, EventArgs e)
        {
            trayIcon.ContextMenuStrip.Hide();
            itemWebUI_Click(sender, e);
        }

        private void itemWebUI_Click(object sender, EventArgs e)
        {
            string url = $"http://{IP}:{_port}";

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
            if (itemAutostart.Checked)
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                if (_port == "5500")
                {
                    key.SetValue("HTFanControl", Process.GetCurrentProcess().MainModule.FileName);
                }
                else
                {
                    key.SetValue(_instanceName, $@"{Process.GetCurrentProcess().MainModule.FileName} ""{_port}"" ""{_instanceName}""");
                }
            }
            else
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                key.DeleteValue(_instanceName, false);
            }
        }

        private void itemClose_Click(object sender, EventArgs e)
        {
            trayIcon.Dispose();

            try
            {
                DirectoryInfo tmp = new DirectoryInfo(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "tmp"));
                foreach (FileInfo file in tmp.GetFiles())
                {
                    file.Delete();
                }
            }
            catch { }

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