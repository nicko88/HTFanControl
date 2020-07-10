using Microsoft.Win32;

namespace HTFanControl
{
    class WinRegistry
    {
        //stops MS Edge from blocking local IPs that failed to load
        public static void FixMSEdge()
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
    }
}