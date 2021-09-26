using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace HTFanControl.Controllers
{
    class LIRCController : IController
    {
        private Socket _lircSocket;
        private Dictionary<string, string> _lircMapping;
        private readonly string _rootPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);

        private string _IP;
        private string _port;
        private string _lircRemote;
        private string _irChannels = "";

        public string ErrorStatus { get; private set; }

        public LIRCController(string IP, string port, string lircRemote, string irChan1, string irChan2, string irChan3, string irChan4)
        {
            _IP = IP;
            _port = port;
            _lircRemote = lircRemote;

            if (irChan1 == "true")
            {
                _irChannels += "1 ";
            }
            if (irChan2 == "true")
            {
                _irChannels += "2 ";
            }
            if (irChan3 == "true")
            {
                _irChannels += "3 ";
            }
            if (irChan4 == "true")
            {
                _irChannels += "4 ";
            }

            LoadLIRCMapping();
        }

        public bool Connect()
        {
            if (_lircSocket != null)
            {
                try
                {
                    _lircSocket.Shutdown(SocketShutdown.Both);
                    _lircSocket.Close();
                }
                catch { }
            }

            try
            {
                IPAddress ipAddress = IPAddress.Parse(_IP);
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, Convert.ToInt32(_port));
                _lircSocket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                IAsyncResult result = _lircSocket.BeginConnect(remoteEP, null, null);
                result.AsyncWaitHandle.WaitOne(3000);

                if (!_lircSocket.Connected)
                {
                    throw new Exception();
                }

                _lircSocket.EndConnect(result);

                Thread.Sleep(25);
                SetTransmitters();
            }
            catch
            {
                ErrorStatus = $"({DateTime.Now:h:mm:ss tt}) Cannot connect to LIRC at: {_IP}:{_port}";
                return false;
            }

            Thread.Sleep(25);

            return true;
        }

        public bool SendCMD(string cmd)
        {
            if (cmd == "STOP")
            {
                if (_lircMapping != null && _lircMapping.TryGetValue("STOP", out string stopCMD))
                {
                    cmd = stopCMD;
                }
                else
                {
                    cmd = "OFF";
                }
            }

            string[] cmds = cmd.Split(',');
            bool goodResult = true;

            foreach (string c in cmds)
            {
                if (_lircMapping != null && _lircMapping.TryGetValue(c, out string remote))
                {
                    _lircRemote = remote;
                }

                string lircCMD = $"SEND_ONCE {_lircRemote} {c}\n";
                goodResult = SendLIRCBytes(Encoding.ASCII.GetBytes(lircCMD));
            }

            if(!goodResult)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private bool SendLIRCBytes(byte[] cmd)
        {
            bool tryAgain = false;
            try
            {
                _lircSocket.Send(cmd);

                if ((_lircSocket.Poll(1000, SelectMode.SelectRead) && (_lircSocket.Available == 0)) || !_lircSocket.Connected)
                {
                    throw new Exception();
                }
            }
            catch
            {
                tryAgain = true;
            }

            if (tryAgain)
            {
                try
                {
                    Thread.Sleep(75);
                    Connect();

                    _lircSocket.Send(cmd);

                    if ((_lircSocket.Poll(1000, SelectMode.SelectRead) && (_lircSocket.Available == 0)) || !_lircSocket.Connected)
                    {
                        throw new Exception();
                    }
                }
                catch
                {
                    ErrorStatus = $"({DateTime.Now:h:mm:ss tt}) Failed sending command to LIRC at: {_IP}:{_port}";
                    return false;
                }
            }

            return true;
        }

        private void SetTransmitters()
        {
            if (ConfigHelper.GetOS() != "win")
            {
                SendLIRCBytes(Encoding.ASCII.GetBytes($"SET_TRANSMITTERS {_irChannels}\n"));
                Console.WriteLine($"SET_TRANSMITTERS {_irChannels}\n");
            }
        }

        private void LoadLIRCMapping()
        {
            _lircMapping = null;
            if (File.Exists(Path.Combine(_rootPath, "lircmapping.txt")))
            {
                _lircMapping = new Dictionary<string, string>();
                string[] mappingFile = File.ReadAllLines(Path.Combine(_rootPath, "lircmapping.txt"));

                foreach (string s in mappingFile)
                {
                    try
                    {
                        string[] vals = s.Split('=');
                        _lircMapping.Add(vals[1], vals[0]);
                    }
                    catch { }
                }
            }
        }
    }
}