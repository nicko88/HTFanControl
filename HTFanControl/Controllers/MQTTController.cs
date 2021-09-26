using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using System;

namespace HTFanControl.Controllers
{
    class MQTTController : IController
    {
        private IMqttClient _mqttClient;

        private string _IP;
        private string _port;
        private string _Topic;
        private string _OFFcmd, _ECOcmd, _LOWcmd, _MEDcmd, _HIGHcmd;

        public string ErrorStatus { get; private set; }

        public MQTTController(string IP, string port, string topic, string OFFcmd, string ECOcmd, string LOWcmd, string MEDcmd, string HIGHcmd)
        {
            _IP = IP;
            _port = port;
            _Topic = topic;
            _OFFcmd = OFFcmd;
            _ECOcmd = ECOcmd;
            _LOWcmd = LOWcmd;
            _MEDcmd = MEDcmd;
            _HIGHcmd = HIGHcmd;
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
                if (!string.IsNullOrEmpty(_port))
                {
                    port = Convert.ToInt32(_port);
                }

                IMqttClientOptions options = new MqttClientOptionsBuilder()
                    .WithTcpServer(_IP, port)
                    .Build();

                IAsyncResult result = _mqttClient.ConnectAsync(options);
                result.AsyncWaitHandle.WaitOne(3000);

                if(!_mqttClient.IsConnected)
                {
                    ErrorStatus = $"({DateTime.Now:h:mm:ss tt}) Failed to connect to MQTT broker at: {_IP}:{_port}";
                    return false;
                }
            }
            catch
            {
                ErrorStatus = $"({DateTime.Now:h:mm:ss tt}) Cannot connect to MQTT broker at: {_IP}:{_port}";
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

            string mqttCMD = cmd switch
            {
                "OFF" => _OFFcmd,
                "ECO" => _ECOcmd,
                "LOW" => _LOWcmd,
                "MED" => _MEDcmd,
                "HIGH" => _HIGHcmd,
                _ => _OFFcmd,
            };

            try
            {
                MqttApplicationMessage message = new MqttApplicationMessageBuilder()
                    .WithTopic(_Topic)
                    .WithPayload(mqttCMD)
                    .Build();

                IAsyncResult result = _mqttClient.PublishAsync(message);
                result.AsyncWaitHandle.WaitOne(3000);
            }
            catch
            {
                ErrorStatus = @$"({DateTime.Now:h:mm:ss tt}) Failed sending Topic: ""{_Topic}"" and Payload: ""{mqttCMD}"" To: {_IP}:{_port}";
                return false;
            }

            return true;
        }
    }
}