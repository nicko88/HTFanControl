using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using System;
using System.Threading;
using HTFanControl.Util;

namespace HTFanControl.Controllers
{
    class MQTTController : IController
    {
        private IMqttClient _mqttClient;
        private Settings _settings;
        private bool _isOFF = true;
        private bool _ONcmd = false;

        public string ErrorStatus { get; private set; }

        public MQTTController(Settings settings)
        {
            _settings = settings;
            _ONcmd = _settings.MQTT_ON_Delay > 0;
        }

        public bool SendCMD(string cmd)
        {
            bool send = true;

            if (!_mqttClient.IsConnected)
            {
                Connect();
            }

            string MQTT_Topic;
            string MQTT_Payload;

            if (!_settings.MQTT_Advanced_Mode)
            {
                MQTT_Topic = cmd switch
                {
                    "OFF" => _settings.MQTT_OFF_Topic,
                    "ECO" => _settings.MQTT_ECO_Topic,
                    "LOW" => _settings.MQTT_LOW_Topic,
                    "MED" => _settings.MQTT_MED_Topic,
                    "HIGH" => _settings.MQTT_HIGH_Topic,
                    _ => null,
                };
                MQTT_Payload = cmd switch
                {
                    "OFF" => _settings.MQTT_OFF_Payload,
                    "ECO" => _settings.MQTT_ECO_Payload,
                    "LOW" => _settings.MQTT_LOW_Payload,
                    "MED" => _settings.MQTT_MED_Payload,
                    "HIGH" => _settings.MQTT_HIGH_Payload,
                    _ => null,
                };
            }
            else
            {
                _settings.MQTT_Topics.TryGetValue(cmd, out MQTT_Topic);
                _settings.MQTT_Payloads.TryGetValue(cmd, out MQTT_Payload);
            }

            //case when using IR over MQTT and fan needs to be turned ON before a command can be sent
            if (_ONcmd && _isOFF)
            {
                if(cmd != "OFF")
                {
                    string ON_Topic = null;
                    string ON_Payload = null;

                    try
                    {
                        if (!_settings.MQTT_Advanced_Mode)
                        {
                            ON_Topic = _settings.MQTT_ON_Topic;
                            ON_Payload = _settings.MQTT_ON_Payload;
                        }
                        else
                        {
                            _settings.MQTT_Topics.TryGetValue("ON", out ON_Topic);
                            _settings.MQTT_Payloads.TryGetValue("ON", out ON_Payload);
                        }

                        MqttApplicationMessage message = new MqttApplicationMessageBuilder()
                            .WithTopic(ON_Topic)
                            .WithPayload(ON_Payload)
                            .Build();

                        _mqttClient.PublishAsync(message);
                    }
                    catch
                    {
                        ErrorStatus = @$"({DateTime.Now:h:mm:ss tt}) Failed turning fan ON by sending Topic: ""{ON_Topic}"" and Payload: ""{ON_Payload}"" To: {_settings.MQTT_IP}:{_settings.MQTT_Port}";
                        return false;
                    }

                    Thread.Sleep(_settings.MQTT_ON_Delay);
                }
                //fan is already OFF, but it's being asked to turn OFF, so don't send a payload, because that would cause it to turn ON again if ON/OFF are the same IR command
                else
                {
                    send = false;
                }
            }

            if (send)
            {
                try
                {
                    MqttApplicationMessage message = new MqttApplicationMessageBuilder()
                        .WithTopic(MQTT_Topic)
                        .WithPayload(MQTT_Payload)
                        .Build();

                    IAsyncResult result = _mqttClient.PublishAsync(message);
                    result.AsyncWaitHandle.WaitOne(1000);
                }
                catch
                {
                    ErrorStatus = @$"({DateTime.Now:h:mm:ss tt}) Failed sending Topic: ""{MQTT_Topic}"" and Payload: ""{MQTT_Payload}"" To: {_settings.MQTT_IP}:{_settings.MQTT_Port}";
                    return false;
                }
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

        public bool Connect()
        {
            Disconnect();

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
                    .WithCredentials(_settings.MQTT_User, _settings.MQTT_Pass)
                    .Build();

                IAsyncResult result = _mqttClient.ConnectAsync(options);
                result.AsyncWaitHandle.WaitOne(3000);

                if (!_mqttClient.IsConnected)
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

        public void Disconnect()
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
        }
    }
}