﻿using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using System;

namespace HTFanControl.Controllers
{
    class MQTTController : IController
    {
        private IMqttClient _mqttClient;
        private Settings _settings;

        public string ErrorStatus { get; private set; }

        public MQTTController(Settings settings)
        {
            _settings = settings;
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

            try
            {
                MqttApplicationMessage message = new MqttApplicationMessageBuilder()
                    .WithTopic(mqttTopic)
                    .WithPayload(mqttPayload)
                    .Build();

                IAsyncResult result = _mqttClient.PublishAsync(message);
                result.AsyncWaitHandle.WaitOne(3000);
            }
            catch
            {
                ErrorStatus = @$"({DateTime.Now:h:mm:ss tt}) Failed sending Topic: ""{mqttTopic}"" and Payload: ""{mqttPayload}"" To: {_settings.MQTT_IP}:{_settings.MQTT_Port}";
                return false;
            }

            return true;
        }
    }
}