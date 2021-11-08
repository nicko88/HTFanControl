using HTFanControl.Players;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using HTFanControl.Timers;
using System.IO.Compression;
using HTFanControl.Controllers;

namespace HTFanControl
{
    class HTFanControl
    {
        public readonly string _rootPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);

        public string _errorStatus;
        public string _windtrackError;
        public long _currentVideoTime = 0;
        public string _currentVideoFileName;
        public string _windTrackHeader;
        public int _curCmdIndex = -1;
        public int _nextCmdIndex = 0;
        public bool _isPlaying = false;
        public bool _isEnabled = true;
        public bool _hasOffset = false;
        public double _offset = 0;
        public bool _offsetEnabled = false;

        public List<Tuple<TimeSpan, string>> _videoTimeCodes;

        private PositionTimer _videoTimer;
        private readonly Timer _syncTimer;
        private IPlayer _mediaPlayer;
        public AudioSync _audioSync;
        public IController _fanController;

        public Settings _settings;

        public HTFanControl()
        {
            _settings = Settings.LoadSettings();
            Settings.SaveSettings(_settings);

            _syncTimer = new Timer(SyncTimerTick, null, Timeout.Infinite, Timeout.Infinite);
            SelectSyncSource();

            SelectController();
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

            if (_settings.MediaPlayerType == "Audio")
            {
                _audioSync?.Stop();
            }

            SelectSyncSource();

            if (fullRefresh)
            {
                SelectController();
            }
        }

        private void SelectController()
        {
            _fanController = _settings.ControllerType switch
            {
                "LIRC" => new LIRCController(_settings),
                "MQTT" => new MQTTController(_settings),
                _ => new LIRCController(_settings),
            };

            if(!_fanController.Connect())
            {
                _errorStatus = _fanController.ErrorStatus;
            }
        }

        private void SelectSyncSource()
        {
            if (_settings.MediaPlayerType == "Audio")
            {
                _audioSync = new AudioSync(this);
                _syncTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            else
            {
                _mediaPlayer = _settings.MediaPlayerType switch
                {
                    "MPC" => new MPCPlayer(_settings),
                    "Kodi" => new KodiPlayer(_settings),
                    "KodiMPC" => new KodiPlayer(_settings),
                    "Plex" => new PlexPlayer(_settings),
                    _ => new MPCPlayer(_settings),
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

                if (!_fanController.SendCMD("OFF"))
                {
                    _errorStatus = _fanController.ErrorStatus;
                }
                Console.WriteLine("Fans Disabled");
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
                            if (!_fanController.SendCMD(_videoTimeCodes[_curCmdIndex].Item2))
                            {
                                _errorStatus = _fanController.ErrorStatus;
                            }
                            Console.WriteLine($"Sent CMD: {_videoTimeCodes[_curCmdIndex].Item1.ToString("G").Substring(2, 12)},{_videoTimeCodes[_curCmdIndex].Item2}");
                        }
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

            if (_settings.MediaPlayerType != "Audio")
            {
                _syncTimer.Change(1000, Timeout.Infinite);
            }
        }

        private void SendCmd(PositionTimer videoTimer, (string cmd, int index) command)
        {
            (string fanCmd, int i) = command;
            _curCmdIndex = i;

            if(i == -1)
            {
                if(_fanController is LIRCController)
                {
                    fanCmd = "STOP";
                }
                else
                {
                    fanCmd = "OFF";
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
                if (!_fanController.SendCMD(fanCmd))
                {
                    _errorStatus = _fanController.ErrorStatus;
                }
            }

            Thread.Sleep(250);
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

                _videoTimeCodes = new List<Tuple<TimeSpan, string>>();
                _windTrackHeader = "";

                string[] lines = File.ReadAllLines(validFilePath);
                _offset = 0;
                string lastCmd = "OFF";
                double rawPrevTime = -500;
                double actualPrevTime = -500;
                bool verifyOrder = true;

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
                                _hasOffset = true;
                            }
                            catch { }
                        }

                        if (line.Contains("ignoreorder"))
                        {
                            verifyOrder = false;
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
                                timeCode = TimeSpan.Parse(lineData[0]).TotalMilliseconds - _settings.GlobalOffsetMS;
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
                            //detect out of order line and error
                            if (verifyOrder)
                            {
                                if (timeCode < rawPrevTime)
                                {
                                    if (_windtrackError is null)
                                    {
                                        _windtrackError = $"Timecode on line {i + 1} is out of order";
                                    }
                                    break;
                                }
                            }

                            rawPrevTime = (double)timeCode;

                            if (isFanCmd)
                            {
                                //if command comes after OFF, add spinup offset
                                if (lastCmd.Contains("OFF"))
                                {
                                    timeCode -= _settings.SpinupOffsetMS;
                                }
                                //if command is OFF, add spindown offset
                                else if (lineData[1].Contains("OFF"))
                                {
                                    timeCode -= _settings.SpindownOffsetMS;
                                }
                                //if offset makes timecode invalid, fix it
                                if (timeCode < actualPrevTime + 500)
                                {
                                    timeCode = actualPrevTime + 500;
                                }
                            }

                            //ignore offset if it's not enabled
                            if(!_offsetEnabled)
                            {
                                _offset = 0;
                            }

                            //keep clearing the list if the timecode is less than or equal to 0 so that we only end up with 1 timecode at 0 at the start
                            if ((timeCode + _offset) <= 0)
                            {
                                _videoTimeCodes.Clear();
                                timeCode = 0;
                            }

                            string cmds = "";
                            for (int j = 1; j < lineData.Length; j++)
                            {
                                cmds += lineData[j] + ",";
                            }

                            _videoTimeCodes.Add(new Tuple<TimeSpan, string>(TimeSpan.FromMilliseconds((double)timeCode + _offset), cmds.Trim(',')));

                            if (isFanCmd)
                            {
                                lastCmd = lineData[1];
                                actualPrevTime = (double)timeCode;
                            }
                        }
                    }
                }

                //sort timecodes just to be safe for special cases where they could be out of order
                _videoTimeCodes = _videoTimeCodes.OrderBy(v => v.Item1).ToList();
            }
            else
            {
                _videoTimeCodes = null;
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