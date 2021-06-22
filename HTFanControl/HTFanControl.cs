using HTFanControl.Players;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using HTFanControl.Timers;
using System.IO.Compression;

namespace HTFanControl
{
    class HTFanControl
    {
        private string _OS = ConfigHelper.GetOS();
        public string _errorStatus;
        public string _windtrackError;
        public long _currentVideoTime = 0;
        public string _currentVideoFileName;
        public string _windTrackHeader;
        public int _curCmdIndex = -1;
        public int _nextCmdIndex = 0;
        public bool _isPlaying = false;
        public bool _isEnabled = true;
        public double _offset = 0;
        public bool _offsetEnabled = false;

        public readonly string _rootPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);

        public List<Tuple<TimeSpan, string>> _videoTimeCodes;
        private Dictionary<string, string> _lircMapping;

        public string _mediaPlayerType = "MPC";
        public string _mediaPlayerIP = "127.0.0.1";
        public string _mediaPlayerPort = "13579";
        public string _lircIP = "127.0.0.1";
        public string _lircPort = "8765";
        public string _lircRemote = "EHF10127B";
        public string _audioDevice;
        public string _PlexToken;
        public string _plexClientName;
        public string _plexClientIP;
        public string _plexClientPort;
        public string _plexClientGUID;
        public string _globalOffsetMS = "2500";
        public string _spinupOffsetMS = "1500";
        public string _spindownOffsetMS = "0";
        public string _irChan1 = "true";
        public string _irChan2 = "false";
        public string _irChan3 = "false";
        public string _irChan4 = "false";

        private PositionTimer _videoTimer;
        private readonly Timer _syncTimer;
        private IPlayer _mediaPlayer;

        public AudioSync _audioSync;

        private Socket _lircSocket;

        public HTFanControl()
        {
            LoadSettings();
            SaveSettings();

            LoadLIRCMapping();

            _syncTimer = new Timer(SyncTimerTick, null, Timeout.Infinite, Timeout.Infinite);
            SelectSyncSource();

            ConnectToLIRC();
            SetTransmitters();
        }

        public void ReInitialize(bool fullRefresh)
        {
            _videoTimer?.Stop();
            _currentVideoFileName = "";
            _windTrackHeader = "";
            _currentVideoTime = 0;
            _videoTimeCodes = null;
            _curCmdIndex = -1;
            _nextCmdIndex = 0;
            _isPlaying = false;

            if (_mediaPlayerType == "Audio")
            {
                _audioSync?.Stop();
            }

            SelectSyncSource();

            if(fullRefresh)
            {
                LoadLIRCMapping();
                SetTransmitters();
            }
        }

        private void LoadSettings()
        {
            try
            {
                string[] settingsFile = File.ReadAllLines(Path.Combine(_rootPath, "HTFanControlSettings.txt"));
                Dictionary<string, string> settings = settingsFile.ToDictionary(x => x.Split('=')[0], x => x.Split('=')[1]);

                if(!settings.TryGetValue("MediaPlayer", out _mediaPlayerType))
                {
                    _mediaPlayerType = "MPC";
                }
                if(!settings.TryGetValue("MediaPlayerIP", out _mediaPlayerIP))
                {
                    _mediaPlayerIP = "127.0.0.1";
                }
                if(!settings.TryGetValue("MediaPlayerPort", out _mediaPlayerPort))
                {
                    _mediaPlayerPort = "13579";
                }
                if(!settings.TryGetValue("LircIP", out _lircIP))
                {
                    _lircIP = "127.0.0.1";
                }
                if(!settings.TryGetValue("LircPort", out _lircPort))
                {
                    _lircPort = "8765";
                }
                if (!settings.TryGetValue("LircRemote", out _lircRemote))
                {
                    _lircRemote = "EHF10127B";
                }

                settings.TryGetValue("AudioDevice", out _audioDevice);

                settings.TryGetValue("PlexToken", out _PlexToken);
                settings.TryGetValue("PlexClientName", out _plexClientName);
                settings.TryGetValue("PlexClientIP", out _plexClientIP);
                settings.TryGetValue("PlexClientPort", out _plexClientPort);
                settings.TryGetValue("PlexClientGUID", out _plexClientGUID);

                if (!settings.TryGetValue("GlobalOffsetMS", out _globalOffsetMS))
                {
                    _globalOffsetMS = "2500";
                }
                if(!settings.TryGetValue("SpinupOffsetMS", out _spinupOffsetMS))
                {
                    _spinupOffsetMS = "1500";
                }
                if (!settings.TryGetValue("SpindownOffsetMS", out _spindownOffsetMS))
                {
                    _spindownOffsetMS = "0";
                }

                if (ConfigHelper.GetOS() != "win")
                {
                    if(!settings.TryGetValue("IRChan1", out _irChan1))
                    {
                        _irChan1 = "true";
                    }
                    if(!settings.TryGetValue("IRChan2", out _irChan2))
                    {
                        _irChan2 = "false";
                    }
                    if(!settings.TryGetValue("IRChan3", out _irChan3))
                    {
                        _irChan3 = "false";
                    }
                    if(!settings.TryGetValue("IRChan4", out _irChan4))
                    {
                        _irChan4 = "false";
                    }
                }
            }
            catch (Exception e)
            {
                _errorStatus = $"({DateTime.Now:h:mm:ss tt}) Failed to load settings.\n\n{e.Message}";
            }
        }

        public void SaveSettings()
        {
            try
            {
                List<string> settings = new List<string>
                {
                    "MediaPlayer=" + _mediaPlayerType,
                    "MediaPlayerIP=" + _mediaPlayerIP,
                    "MediaPlayerPort=" + _mediaPlayerPort,
                    "LircIP=" + _lircIP,
                    "LircPort=" + _lircPort,
                    "LircRemote=" + _lircRemote,
                    "AudioDevice=" + _audioDevice,
                    "PlexToken=" + _PlexToken,
                    "PlexClientName=" + _plexClientName,
                    "PlexClientIP=" + _plexClientIP,
                    "PlexClientPort=" + _plexClientPort,
                    "PlexClientGUID=" + _plexClientGUID,
                    "GlobalOffsetMS=" + _globalOffsetMS,
                    "SpinupOffsetMS=" + _spinupOffsetMS,
                    "SpindownOffsetMS=" + _spindownOffsetMS
                };

                if (ConfigHelper.GetOS() != "win")
                {
                    settings.Add("IRChan1=" + _irChan1);
                    settings.Add("IRChan2=" + _irChan2);
                    settings.Add("IRChan3=" + _irChan3);
                    settings.Add("IRChan4=" + _irChan4);
                }

                File.WriteAllLines(Path.Combine(_rootPath, "HTFanControlSettings.txt"), settings);

                SetTransmitters();
            }
            catch (Exception e)
            {
                _errorStatus = $"({DateTime.Now:h:mm:ss tt}) Failed to save settings.\n\n{e.Message}";
            }
        }

        private void SelectSyncSource()
        {
            if (_mediaPlayerType == "Audio")
            {
                _audioSync = new AudioSync(this);
                _syncTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            else
            {
                _mediaPlayer = _mediaPlayerType switch
                {
                    "MPC" => new MPCPlayer(_mediaPlayerIP, _mediaPlayerPort),
                    "Kodi" => new KodiPlayer(_mediaPlayerIP, _mediaPlayerPort, false),
                    "KodiMPC" => new KodiPlayer(_mediaPlayerIP, _mediaPlayerPort, true),
                    "Plex" => new PlexPlayer(_mediaPlayerIP, _mediaPlayerPort, _PlexToken, _plexClientIP, _plexClientPort, _plexClientGUID, _plexClientName),
                    _ => new MPCPlayer(_mediaPlayerIP, _mediaPlayerPort),
                };

                _syncTimer.Change(1000, Timeout.Infinite);
            }
        }

        public async void SelectVideo(string fileName)
        {
            ExtractWindtrack(Path.Combine(_rootPath, "windtracks", fileName + ".zip"), true);

            _currentVideoFileName = "Loading Video Fingerprints...";
            _windTrackHeader = "Loading Windtrack...";

            _audioSync.Start(fileName);
            _currentVideoFileName = fileName;
            LoadVideoTimecodes(fileName, "");

            if (_videoTimer != null)
            {
                await _videoTimer.DisposeAsync(_videoTimeCodes == null);
            }
            if (_videoTimeCodes != null)
            {
                _videoTimer = new PositionTimer<(string, int)>(_videoTimeCodes.Select((v, i) => (v.Item1, (v.Item2, i))), SendCmd, 1000, ("OFF", -1));
            }
            else
            {
                _videoTimer = null;
            }
        }

        public void UpdateTime()
        {
            _videoTimer.Update(TimeSpan.FromMilliseconds(_currentVideoTime));
        }

        public void ToggleFan()
        {
            if (_isEnabled)
            {
                _isEnabled = false;

                byte[] cmd = Encoding.ASCII.GetBytes($"SEND_ONCE {_lircRemote} OFF\n");

                Console.WriteLine("Fans Disabled");
                SendToLIRC(cmd);
            }
            else
            {
                Console.WriteLine("Fans Enabled");

                _isEnabled = true;

                if (_videoTimer != null)
                {
                    try
                    {
                        if (_isPlaying)
                        {
                            byte[] cmd = Encoding.ASCII.GetBytes($"SEND_ONCE {_lircRemote} {_videoTimeCodes[_curCmdIndex].Item2}\n");
                            Console.WriteLine($"Sent CMD: {_videoTimeCodes[_curCmdIndex].Item1.ToString("G").Substring(2, 12)},{_videoTimeCodes[_curCmdIndex].Item2}");

                            SendToLIRC(cmd);
                        }
                    }
                    catch { }
                }
            }
        }

        private void LoadLIRCMapping()
        {
            _lircMapping = null;
            if (File.Exists(Path.Combine(_rootPath, "lircmapping.txt")))
            {
                _lircMapping = new Dictionary<string, string>();
                string[] mappingFile = File.ReadAllLines(Path.Combine(_rootPath, "lircmapping.txt"));

                foreach(string s in mappingFile)
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

        private void SyncTimerTick(object o)
        {
            SyncVideo();
        }

        private async void SyncVideo()
        {
            if (_mediaPlayer != null)
            {
                bool success = _mediaPlayer.Update();

                if (success)
                {
                    _isPlaying = _mediaPlayer.IsPlaying;
                    _currentVideoTime = _mediaPlayer.VideoTime;

                    if (_currentVideoFileName != _mediaPlayer.FileName)
                    {
                        _currentVideoFileName = _mediaPlayer.FileName;
                        LoadVideoTimecodes(_mediaPlayer.FileName, _mediaPlayer.FilePath);

                        if (_videoTimer != null)
                        {
                            await _videoTimer.DisposeAsync(_videoTimeCodes == null);
                        }
                        if (_videoTimeCodes != null)
                        {
                            _videoTimer = new PositionTimer<(string, int)>(_videoTimeCodes.Select((v, i) => (v.Item1, (v.Item2, i))), SendCmd, _mediaPlayer.VideoTimeResolution, ("OFF", -1));
                        }
                        else
                        {
                            _videoTimer = null;
                        }
                    }

                    if (_videoTimer != null)
                    {
                        if (_mediaPlayer.IsPlaying)
                        {
                            _videoTimer.Update(TimeSpan.FromMilliseconds(_mediaPlayer.VideoTime));
                        }
                        else
                        {
                            _videoTimer.Stop();
                        }
                    }
                }
                else
                {
                    _errorStatus = _mediaPlayer.ErrorStatus;
                    ReInitialize(false);
                }
            }

            if (_mediaPlayerType != "Audio")
            {
                _syncTimer.Change(1000, Timeout.Infinite);
            }
        }

        private void SendCmd(PositionTimer videoTimer, (string cmd, int index) command)
        {
            var (fanCmd, i) = command;
            _curCmdIndex = i;
            if(i == -1)
            {
                fanCmd = "OFF";

                if (_lircMapping != null && _lircMapping.TryGetValue("STOP", out string stopCMD))
                {
                    fanCmd = stopCMD;
                }
            }
            else
            {
                _nextCmdIndex = i + 1;
            }

            try
            {
                Console.WriteLine($"Sent CMD: {_videoTimeCodes[i].Item1.ToString("G").Substring(2, 12)},{fanCmd}");
            }
            catch
            {
                Console.WriteLine($"Sent CMD: {fanCmd}");
            }

            if (_isEnabled)
            {
                string[] cmds = fanCmd.Split(',');

                foreach(string cmd in cmds)
                {
                    if (_lircMapping != null && _lircMapping.TryGetValue(cmd, out string remote))
                    {
                        _lircRemote = remote;
                    }

                    byte[] cmdBytes = Encoding.ASCII.GetBytes($"SEND_ONCE {_lircRemote} {cmd}\n");
                    SendToLIRC(cmdBytes);
                }
            }

            Thread.Sleep(250);
        }

        public void SendToLIRC(byte[] cmd)
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

            if(tryAgain)
            {
                try
                {
                    Thread.Sleep(75);
                    ConnectToLIRC();

                    _lircSocket.Send(cmd);

                    if ((_lircSocket.Poll(1000, SelectMode.SelectRead) && (_lircSocket.Available == 0)) || !_lircSocket.Connected)
                    {
                        throw new Exception();
                    }
                }
                catch
                {
                    _errorStatus = $"({DateTime.Now:h:mm:ss tt}) Failed sending command to LIRC at: {_lircIP}:{_lircPort}";
                }
            }
        }

        private void ConnectToLIRC()
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
                IPAddress ipAddress = IPAddress.Parse(_lircIP);
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, Convert.ToInt32(_lircPort));
                _lircSocket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                IAsyncResult result = _lircSocket.BeginConnect(remoteEP, null, null);
                result.AsyncWaitHandle.WaitOne(1000);

                if (!_lircSocket.Connected)
                {

                    throw new Exception();
                }

                _lircSocket.EndConnect(result);
            }
            catch
            {
                _errorStatus = $"({DateTime.Now:h:mm:ss tt}) Cannot connect to LIRC at: {_lircIP}:{_lircPort}";
            }

            Thread.Sleep(25);
        }

        private string GetWindtrackFilePath(string fileName, string filePath)
        {
            string validFilePath = null;

            //look for windtrack .txt in tmp folder
            try
            {
                if (File.Exists(Path.Combine(_rootPath, "tmp", fileName + ".txt")))
                {
                    validFilePath = Path.Combine(_rootPath, "tmp", fileName + ".txt");
                }
            }
            catch { }

            //look for windtrack .zip archive in windtracks folder
            try
            {
                if (string.IsNullOrEmpty(validFilePath) && File.Exists(Path.Combine(_rootPath, "windtracks", fileName + ".zip")))
                {
                    ExtractWindtrack(Path.Combine(_rootPath, "windtracks", fileName + ".zip"), false);
                    validFilePath = Path.Combine(_rootPath, "tmp", fileName + ".txt");
                }
            }
            catch { }

            //if not found, look in the active video's folder
            try
            {
                if (string.IsNullOrEmpty(validFilePath) && File.Exists(Path.Combine(filePath, fileName + ".zip")))
                {
                    ExtractWindtrack(Path.Combine(filePath, fileName + ".zip"), false);
                    validFilePath = Path.Combine(_rootPath, "tmp", fileName + ".txt");
                }
            }
            catch { }

            //LEGACY look in windtrack folder
            try
            {
                if (string.IsNullOrEmpty(validFilePath) && File.Exists(Path.Combine(_rootPath, "windtracks", fileName + ".txt")))
                {
                    validFilePath = Path.Combine(_rootPath, "windtracks", fileName + ".txt");
                }
            }
            catch { }

            //check the active video's folder
            try
            {
                if (string.IsNullOrEmpty(validFilePath) && File.Exists(Path.Combine(filePath, fileName + ".txt")))
                {
                    validFilePath = Path.Combine(filePath, fileName + ".txt");
                }
            }
            catch { }
 
            return validFilePath;
        }

        private void LoadVideoTimecodes(string fileName, string filePath)
        {
            string validFilePath = GetWindtrackFilePath(fileName, filePath);

            if (!string.IsNullOrEmpty(validFilePath))
            {
                _windtrackError = null;

                SetTransmitters();

                _videoTimeCodes = new List<Tuple<TimeSpan, string>>();
                _windTrackHeader = "";

                string[] lines = File.ReadAllLines(validFilePath);
                _offset = 0;
                string lastCmd = "OFF";
                double rawPrevTime = -500;
                double actualPrevTime = -500;
                double globalOffsetMS = Convert.ToDouble(_globalOffsetMS);
                double spinupOffsetMS = Convert.ToDouble(_spinupOffsetMS);
                double spindownOffsetMS = Convert.ToDouble(_spindownOffsetMS);

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];

                    //header lines
                    if (line.StartsWith("#") && _videoTimeCodes.Count == 0)
                    {
                        _windTrackHeader += line.TrimStart(new[] { '#', ' ' }) + "<br \\>";

                        //offset line
                        if (line.Contains("Offset:"))
                        {
                            try
                            {
                                TimeSpan ts = TimeSpan.Parse(line.Substring(line.IndexOf('(') + 1, line.LastIndexOf(')') - line.IndexOf('(') - 1));
                                _offset = ts.TotalMilliseconds;
                            }
                            catch { }
                        }
                    }
                    //non-comment or blank lines
                    else if (!line.StartsWith(@"\\") && !line.StartsWith(@"//") && line.Length > 0)
                    {
                        string[] lineData = line.Split(',');

                        // check if this timecode contains a fan command so we can apply offsets
                        bool isFanCmd = false;
                        for (int j = 1; j < lineData.Length; j++)
                        {
                            if (lineData[j].Equals("OFF") || lineData[j].Equals("ECO") || lineData[j].Equals("LOW") || lineData[j].Equals("MED") || lineData[j].Equals("HIGH"))
                            {
                                isFanCmd = true;
                            }
                        }

                        double? timeCode = null;
                        try
                        {
                            if (isFanCmd)
                            {
                                timeCode = TimeSpan.Parse(lineData[0]).TotalMilliseconds - globalOffsetMS;
                            }
                            else
                            {
                                timeCode = TimeSpan.Parse(lineData[0]).TotalMilliseconds;
                            }
                        }
                        catch
                        {
                            if (_windtrackError is null)
                            {
                                _windtrackError = $"Bad timecode on line {i+1}: {line}";
                            }
                        }

                        if(timeCode != null)
                        {
                            //apply special offset if enabled
                            if (_offsetEnabled)
                            {
                                timeCode += _offset;
                                rawPrevTime += _offset;
                            }
                            //skip out of order line and error
                            if (timeCode < rawPrevTime)
                            {
                                if (_windtrackError is null)
                                {
                                    _windtrackError = $"Timecode on line {i+1} is out of order";
                                }
                                break;
                            }

                            rawPrevTime = (double)timeCode;

                            if (isFanCmd)
                            {
                                //if command comes after OFF, add spinup offset
                                if (lastCmd.Contains("OFF"))
                                {
                                    timeCode -= spinupOffsetMS;
                                }
                                //if command is OFF, add spindown offset
                                else if (lineData[1].Contains("OFF"))
                                {
                                    timeCode -= spindownOffsetMS;
                                }
                                //if offset makes timecode invalid, fix it
                                if (timeCode < actualPrevTime + 500)
                                {
                                    timeCode = actualPrevTime + 500;
                                }
                            }

                            //keep clearing the list if the timecode is less than or equal to 0 so that we only end up with 1 timecode at 0 at the start
                            if(timeCode <= 0)
                            {
                                _videoTimeCodes.Clear();
                                timeCode = 0;
                            }

                            string cmds = "";
                            for (int j = 1; j < lineData.Length; j++)
                            {
                                cmds += lineData[j] + ",";
                            }

                            _videoTimeCodes.Add(new Tuple<TimeSpan, string>(TimeSpan.FromMilliseconds((double)timeCode), cmds.Trim(',')));

                            if (isFanCmd)
                            {
                                lastCmd = lineData[1];
                                actualPrevTime = (double)timeCode;
                            }
                        }
                    }
                }
            }
            else
            {
                _videoTimeCodes = null;
            }
        }

        private void SetTransmitters()
        {
            if (_OS != "win")
            {
                string irChannels = "";
                if (_irChan1 == "true")
                {
                    irChannels += "1 ";
                }
                if (_irChan2 == "true")
                {
                    irChannels += "2 ";
                }
                if (_irChan3 == "true")
                {
                    irChannels += "3 ";
                }
                if (_irChan4 == "true")
                {
                    irChannels += "4 ";
                }

                byte[] data = Encoding.ASCII.GetBytes($"SET_TRANSMITTERS {irChannels}\n");
                Console.WriteLine($"SET_TRANSMITTERS {irChannels}\n");

                SendToLIRC(data);
            }
        }

        private void ExtractWindtrack(string filePath, bool extractFingerprint)
        {
            DirectoryInfo tmp = new DirectoryInfo(Path.Combine(_rootPath, "tmp"));
            foreach (FileInfo file in tmp.GetFiles())
            {
                file.Delete();
            }

            string fileName = Path.GetFileNameWithoutExtension(filePath);

            try
            {
                using ZipArchive archive = ZipFile.OpenRead(filePath);

                archive.Entries.Where(e => e.Name.Equals("commands.txt")).Single().ExtractToFile(Path.Combine(_rootPath, "tmp", fileName + ".txt"), true);

                if (extractFingerprint)
                {
                    archive.Entries.Where(e => e.Name.Equals("full.fingerprints")).Single().ExtractToFile(Path.Combine(_rootPath, "tmp", fileName + ".fingerprints"), true);
                }
            }
            catch
            {
                _errorStatus = $"Failed to extract windtrack file: {filePath}";
            }
        }
    }
}