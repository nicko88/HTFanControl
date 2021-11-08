using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HTFanControl
{
    class Settings
    {
        private static readonly string _rootPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
        public string MediaPlayerType { get; set; }
        public string MediaPlayerIP { get; set; }
        public int MediaPlayerPort { get; set; }
        public string ControllerType { get; set; }
        public string LIRC_IP { get; set; }
        public int LIRC_Port { get; set; }
        public string LIRC_Remote { get; set; }
        public string MQTT_IP { get; set; }
        public int MQTT_Port { get; set; }
        public string MQTT_OFF_Topic { get; set; }
        public string MQTT_OFF_Payload { get; set; }
        public string MQTT_ECO_Topic { get; set; }
        public string MQTT_ECO_Payload { get; set; }
        public string MQTT_LOW_Topic { get; set; }
        public string MQTT_LOW_Payload { get; set; }
        public string MQTT_MED_Topic { get; set; }
        public string MQTT_MED_Payload { get; set; }
        public string MQTT_HIGH_Topic { get; set; }
        public string MQTT_HIGH_Payload { get; set; }
        public string AudioDevice { get; set; }
        public string PlexToken { get; set; }
        public string PlexClientName { get; set; }
        public string PlexClientIP { get; set; }
        public string PlexClientPort { get; set; }
        public string PlexClientGUID { get; set; }
        public int GlobalOffsetMS { get; set; }
        public int SpinupOffsetMS { get; set; }
        public int SpindownOffsetMS { get; set; }
        public bool IR_CHAN1 { get; set; }
        public bool IR_CHAN2 { get; set; }
        public bool IR_CHAN3 { get; set; }
        public bool IR_CHAN4 { get; set; }

        public static Settings LoadSettings()
        {
            Settings settings = new Settings();
            try
            {
                JsonSerializerOptions options = new JsonSerializerOptions();
                options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
                options.WriteIndented = true;

                string jsonSettings = File.ReadAllText(Path.Combine(_rootPath, "HTFanControlSettings.json"));
                settings = JsonSerializer.Deserialize<Settings>(Path.Combine(_rootPath, "HTFanControlSettings.json"), options);
            }
            catch
            {
                //default values
                settings.MediaPlayerType = "MPC";
                settings.MediaPlayerIP = "127.0.0.1";
                settings.MediaPlayerPort = 13579;
                settings.ControllerType = "LIRC";
                settings.LIRC_IP = "127.0.0.1";
                settings.LIRC_Port = 8765;
                settings.LIRC_Remote = "EHF10127B";
                settings.GlobalOffsetMS = 2500;
                settings.SpinupOffsetMS = 1500;

                if (ConfigHelper.GetOS() != "win")
                {
                    settings.IR_CHAN1 = true;
                }
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

                File.WriteAllText(Path.Combine(_rootPath, "HTFanControlSettings.json"), jsonSettings);
            }
            catch(Exception e)
            {
                error = e.Message;
            }

            return error;
        }
    }
}