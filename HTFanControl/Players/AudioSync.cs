using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SoundFingerprinting.Audio;
using SoundFingerprinting.Builder;
using SoundFingerprinting.Command;
using SoundFingerprinting.InMemory;
using SoundFingerprinting.Query;
using OpenTK.Audio.OpenAL;
using System.IO;
using System.Net.Http;
using System.Linq;
using HTFanControl.Util;

namespace HTFanControl.Main
{
    class AudioSync
    {
        private bool verifyAccuracy = false;
        private string _state;
        private TimeSpan _lastMatchTime;
        private bool _timeJump = false;

        private InMemoryModelService _modelService;

        private CancellationTokenSource tokenSource;
        private BlockingCollection<AudioSamples> _realtimeSource;
        private List<float> _float32Buffer = new List<float>();

        //private Timer _pause;

        private Thread _recordMic;
        private HTFanControl _hTFanControl;

        public string State
        {
            get
            {
                return _state;
            }
        }

        public AudioSync(HTFanControl hTFanControl)
        {
            _hTFanControl = hTFanControl;

            if(File.Exists(Path.Combine(ConfigHelper._rootPath, "testaccuracy.txt")))
            {
                verifyAccuracy = true;
            }
        }

        public void Start(string fileName)
        {
            tokenSource = new CancellationTokenSource();

            LoadFingerprint(fileName);

            _recordMic = new Thread(RecordOpenTK);
            _recordMic.Start(tokenSource.Token);

            //_pause = new Timer(Pause, null, Timeout.Infinite, Timeout.Infinite);

            _lastMatchTime = TimeSpan.MinValue;

            StartMatching(tokenSource.Token);
        }

        public void Stop()
        {
            if (_modelService != null)
            {
                _hTFanControl._log.LogMsg("Stop Listening...");
            }

            _state = "";

            _modelService = null;
            _realtimeSource = null;
            _float32Buffer = new List<float>();

            try
            {
                //_pause.Change(Timeout.Infinite, Timeout.Infinite);
                tokenSource.Cancel();
            }
            catch { }
        }

        private void LoadFingerprint(string fileName)
        {
            string validFilePath = null;
            try
            {
                if (File.Exists(Path.Combine(new string[] { ConfigHelper._rootPath, "tmp", "fingerprint", "audio" })))
                {
                    validFilePath = Path.Combine(new string[] { ConfigHelper._rootPath, "tmp", "fingerprint" });
                }

                _modelService = new InMemoryModelService(validFilePath);
            }
            catch
            {
                _hTFanControl._errorStatus = $"Failed to load audio fingerprints from: {validFilePath}";
            }
        }

        private async void StartMatching(CancellationToken cancellationToken)
        {
            _realtimeSource = new BlockingCollection<AudioSamples>();

            _hTFanControl._log.LogMsg("Start Listening...");
            _state = "(listening...)";

            try
            {
                _ = await GetBestMatchForStream(_realtimeSource, cancellationToken);
            }
            catch { }
        }

        private void FoundMatch(AVQueryResult aVQueryResult)
        {
            if (aVQueryResult.ContainsMatches)
            {
                ResultEntry resultEntry = aVQueryResult.ResultEntries.First().Audio;

                _timeJump = false;
                TimeSpan matchTime = TimeSpan.FromSeconds(resultEntry.TrackMatchStartsAt + resultEntry.QueryLength + 0.2 /*+ TimeSpan.FromMilliseconds(aVQueryResult.QueryCommandStats.Audio.TotalDurationMilliseconds).TotalSeconds*/);

                if (matchTime > _lastMatchTime.Add(TimeSpan.FromMinutes(5)) || matchTime < _lastMatchTime.Subtract(TimeSpan.FromMinutes(5)))
                {
                    _timeJump = true;
                    _lastMatchTime = matchTime;
                    _hTFanControl._log.LogMsg("Time Jump Detected");
                }

                if (!_timeJump)
                {
                    _hTFanControl._log.LogMsg($"Match Found: {matchTime.ToString("G").Substring(2, 12)}");
                    _hTFanControl._loadedVideoTime = Convert.ToInt64(matchTime.TotalMilliseconds);
                    _hTFanControl.UpdateTime();
                    _lastMatchTime = matchTime;

                    if (verifyAccuracy)
                    {
                        VerifyAccuracy(matchTime);
                    }
                }

                //_pause.Change(10000, Timeout.Infinite);
            }
        }

        private void VerifyAccuracy(TimeSpan audioTime)
        {
            long position = 0;
            try
            {
                HttpClient httpClient = new HttpClient();
                string html = httpClient.GetStringAsync($"http://{_hTFanControl._settings.MediaPlayerIP}:{_hTFanControl._settings.MediaPlayerPort}/variables.html").Result;

                HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);

                position = long.Parse(doc.GetElementbyId("position").InnerText) + 21;
            }
            catch { }

            TimeSpan playerTime = TimeSpan.FromMilliseconds(position);
            string matchResult = $"Accuracy:{audioTime.Subtract(playerTime).TotalMilliseconds} AudioTime:{audioTime.ToString("G").Substring(2, 12)} PlayerTime:{playerTime.ToString("G").Substring(2, 12)}";
            _hTFanControl._log.LogMsg(matchResult);
        }

        //private void Pause(object o)
        //{
        //    _pause.Change(Timeout.Infinite, Timeout.Infinite);
        //    _hTFanControl._log.LogMsg("PAUSED");
        //}

        private void RecordOpenTK(object cancellationToken)
        {
            CancellationToken token = (CancellationToken)cancellationToken;

            ALCaptureDevice captureDevice = ALC.CaptureOpenDevice(_hTFanControl._settings.AudioDevice, 11024, ALFormat.Mono16, 10240);
            {
                ALC.CaptureStart(captureDevice);

                while (true)
                {
                    try
                    {
                        token.ThrowIfCancellationRequested();

                        //wait for some audio samples to accumulate
                        Thread.Sleep(100);

                        if (captureDevice.Handle != IntPtr.Zero)
                        {
                            int samplesAvailable = ALC.GetAvailableSamples(captureDevice);

                            if (samplesAvailable > 0)
                            {
                                short[] samples = new short[samplesAvailable];
                                ALC.CaptureSamples(captureDevice, ref samples[0], samplesAvailable);

                                for (int i = 0; i < samples.Length; i += 2)
                                {
                                    _float32Buffer.Add(samples[i] / 32767f);
                                }

                                _realtimeSource.Add(new AudioSamples(_float32Buffer.ToArray(), string.Empty, 5512));
                                _float32Buffer = new List<float>();
                            }
                        }
                        else
                        {
                            _hTFanControl._errorStatus = $"Failed to record from audio input device: {_hTFanControl._settings.AudioDevice}";
                        }
                    }
                    catch
                    {
                        ALC.CaptureStop(captureDevice);
                        ALC.CaptureCloseDevice(captureDevice);
                        break;
                    }
                }
            }
        }

        public async Task<double> GetBestMatchForStream(BlockingCollection<AudioSamples> audioSamples, CancellationToken token)
        {
            double seconds = await QueryCommandBuilder.Instance
                                    .BuildRealtimeQueryCommand()
                                    .From(new BlockingRealtimeCollection<AudioSamples>(audioSamples))
                                    .WithRealtimeQueryConfig(config =>
                                    {
                                        config.ResultEntryFilter = new TrackMatchLengthEntryFilter(3d);
                                        config.SuccessCallback = result => FoundMatch(result);
                                        config.AutomaticSkipDetection = true;
                                        return config;
                                    })
                                    .UsingServices(_modelService)
                                    .Query(token);
            return seconds;
        }
    }
}