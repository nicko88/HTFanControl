using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using System;
using System.Threading;

namespace HTFanControl.Controllers
{
    class MQTTController : IController
    {
        private IMqttClient _mqttClient;
        private Settings _settings;
        private bool _isOFF = true;
        private bool _specialONcmd = false;

        public string ErrorStatus { get; private set; }

        public MQTTController(Settings settings)
        {
            _settings = settings;
            _specialONcmd = !string.IsNullOrEmpty(_settings.MQTT_ON_Topic);
        }

        public bool Connect()
        {
            if (_mqttClient != null)
            {
                try
                {
                    _mqttClient.DisconnectAsync();
                    _mqttClient.Dispose();
                }
                catch { }
            }

            try
            {
                MqttFactory factory = new MqttFactory();
                _mqttClient = factory.CreateMqttClient();

                int? port = null;
                if (_settings.MQTT_Port != 0)
                {
                    port = _settings.MQTT_Port;
                }

                IMqttClientOptions options = new MqttClientOptionsBuilder()
                    .WithTcpServer(_settings.MQTT_IP, port)
                    .Build();

                IAsyncResult result = _mqttClient.ConnectAsync(options);
                result.AsyncWaitHandle.WaitOne(3000);

                if(!_mqttClient.IsConnected)
                {
                    ErrorStatus = $"({DateTime.Now:h:mm:ss tt}) Failed to connect to MQTT broker at: {_settings.MQTT_IP}:{_settings.MQTT_Port}";
                    return false;
                }
            }
            catch
            {
                ErrorStatus = $"({DateTime.Now:h:mm:ss tt}) Cannot connect to MQTT broker at: {_settings.MQTT_IP}:{_settings.MQTT_Port}";
                return false;
            }

            return true;
        }

        public bool SendCMD(string cmd)
        {
            if (!_mqttClient.IsConnected)
            {
                Connect();
            }

            string mqttTopic = cmd switch
            {
                "OFF" => _settings.MQTT_OFF_Topic,
                "ECO" => _settings.MQTT_ECO_Topic,
                "LOW" => _settings.MQTT_LOW_Topic,
                "MED" => _settings.MQTT_MED_Topic,
                "HIGH" => _settings.MQTT_HIGH_Topic,
                _ => _settings.MQTT_OFF_Topic,
            };
            string mqttPayload = cmd switch
            {
                "OFF" => _settings.MQTT_OFF_Payload,
                "ECO" => _settings.MQTT_ECO_Payload,
                "LOW" => _settings.MQTT_LOW_Payload,
                "MED" => _settings.MQTT_MED_Payload,
                "HIGH" => _settings.MQTT_HIGH_Payload,
                _ => _settings.MQTT_OFF_Payload,
            };

            //special case for someone using IR over MQTT and their fan needs to be turned on before it can be set to a speed.
            if(_specialONcmd && _isOFF)
            {
                if(cmd != "OFF")
                {
                    MqttApplicationMessage message = new MqttApplicationMessageBuilder()
                        .WithTopic(_settings.MQTT_ON_Topic)
                        .WithPayload(_settings.MQTT_ON_Payload)
                        .Build();

                    _mqttClient.PublishAsync(message);

                    Thread.Sleep(_settings.MQTT_ON_Delay);
                }
                else
                {
                    mqttPayload = "";
                    Console.WriteLine("(Ignored OFF Command)");
                }
            }

            try
            {
                MqttApplicationMessage message = new MqttApplicationMessageBuilder()
                    .WithTopic(mqttTopic)
                    .WithPayload(mqttPayload)
                    .Build();

                IAsyncResult result = _mqttClient.PublishAsync(message);
                result.AsyncWaitHandle.WaitOne(1000);
            }
            catch
            {
                ErrorStatus = @$"({DateTime.Now:h:mm:ss tt}) Failed sending Topic: ""{mqttTopic}"" and Payload: ""{mqttPayload}"" To: {_settings.MQTT_IP}:{_settings.MQTT_Port}";
                return false;
            }

            if (cmd == "OFF")
            {
                _isOFF = true;
            }
            else
            {
                _isOFF = false;
            }

            return true;
        }
    }
}