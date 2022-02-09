using Microsoft.Win32;

namespace HTFanControl.Util
{
    class WinRegistry
    {
        //stops MS Edge from blocking local IPs that failed to load
        public static void FixMSEdge()
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey("Software\\Classes\\Local Settings\\Software\\Microsoft\\Windows\\CurrentVersion\\AppContainer\\Storage\\microsoft.microsoftedge_8wekyb3d8bbwe\\MicrosoftEdge\\TabProcConfig", true);
                if (key != null)
                {
                    string[] values = key.GetValueNames();

                    foreach (string value in values)
                    {
                        key.DeleteValue(value, false);
                    }
                }
            }
            catch { }
        }

        //if MPC-HC is running on the same system as HTFanControl, this tries to set MPC-HC timer interval to the quickest interval which makes HTFanControl more accurate
        public static void SetMPCHCTimerInterval()
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey("Software\\MPC-HC\\MPC-HC\\Settings", true);

                if (key != null)
                {
                    object value = key.GetValue("TimeRefreshInterval");

                    if (value != null)
                    {
                        key.SetValue("TimeRefreshInterval", 40);
                    }
                }
            }
            catch { }
        }
    }
}