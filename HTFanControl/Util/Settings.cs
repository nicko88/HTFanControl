using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace HTFanControl.Util
{
    class Settings
    {
        public string MediaPlayerType { get; set; }
        public string MediaPlayerIP { get; set; }
        public int MediaPlayerPort { get; set; }
        public string ControllerType { get; set; }
        public string LIRC_IP { get; set; }
        public int LIRC_Port { get; set; }
        public string LIRC_Remote { get; set; }
        public int LIRC_ON_Delay { get; set; }
        public string MQTT_IP { get; set; }
        public int MQTT_Port { get; set; }
        public string MQTT_User { get; set; }
        public string MQTT_Pass { get; set; }
        public Dictionary<string,string> MQTT_Topics { get; set; }
        public Dictionary<string,string> MQTT_Payloads { get; set; }
        public int MQTT_ON_Delay { get; set; }
        public string AudioDevice { get; set; }
        public string PlexToken { get; set; }
        public string PlexClientName { get; set; }
        public string PlexClientIP { get; set; }
        public string PlexClientPort { get; set; }
        public string PlexClientGUID { get; set; }
        public int GlobalOffsetMS { get; set; }
        public int ECOSpinupOffsetMS { get; set; }
        public int LOWSpinupOffsetMS { get; set; }
        public int MEDSpinupOffsetMS { get; set; }
        public int HIGHSpinupOffsetMS { get; set; }
        public int SpindownOffsetMS { get; set; }

        public static Settings LoadSettings()
        {
            Settings settings = new Settings();
            try
            {
                JsonSerializerOptions options = new JsonSerializerOptions();
                options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
                options.WriteIndented = true;

                string jsonSettings = File.ReadAllText(Path.Combine(ConfigHelper._rootPath, "HTFanControlSettings.json"));
                settings = JsonSerializer.Deserialize<Settings>(jsonSettings, options);
            }
            catch
            {
                //default values
                settings.MediaPlayerType = "Kodi";
                settings.MediaPlayerIP = "192.168.1.100";
                settings.MediaPlayerPort = 8080;
                settings.ControllerType = "MQTT";
                settings.MQTT_IP = "127.0.0.1";
                settings.MQTT_Topics = new Dictionary<string, string>();
                settings.MQTT_Topics.Add("OFF", "cmnd/HTFan/EVENT");
                settings.MQTT_Topics.Add("ECO", "cmnd/HTFan/EVENT");
                settings.MQTT_Topics.Add("LOW", "cmnd/HTFan/EVENT");
                settings.MQTT_Topics.Add("MED", "cmnd/HTFan/EVENT");
                settings.MQTT_Topics.Add("HIGH", "cmnd/HTFan/EVENT");
                settings.MQTT_Payloads = new Dictionary<string, string>();
                settings.MQTT_Payloads.Add("OFF", "s0");
                settings.MQTT_Payloads.Add("ECO", "s1");
                settings.MQTT_Payloads.Add("LOW", "s2");
                settings.MQTT_Payloads.Add("MED", "s3");
                settings.MQTT_Payloads.Add("HIGH", "s4");
                settings.GlobalOffsetMS = 2000;
                settings.ECOSpinupOffsetMS = 1400;
                settings.LOWSpinupOffsetMS = 1200;
                settings.MEDSpinupOffsetMS = 1000;
                settings.HIGHSpinupOffsetMS = 800;
                settings.SpindownOffsetMS = 250;
            }

            return settings;
        }

        public static string SaveSettings(Settings settings)
        {
            string error = null;
            try
            {
                JsonSerializerOptions options = new JsonSerializerOptions();
                options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
                options.WriteIndented = true;

                string jsonSettings = JsonSerializer.Serialize(settings, options);

                File.WriteAllText(Path.Combine(ConfigHelper._rootPath, "HTFanControlSettings.json"), jsonSettings);
            }
            catch(Exception e)
            {
                error = e.Message;
            }

            return error;
        }
    }
}
