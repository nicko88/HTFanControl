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

namespace HTFanControl
{
    class AudioSync
    {
        private bool verifyAccuracy = false;
        private string _state;
        private TimeSpan _lastMatchTime;
        private bool _timeJump = false;

        private InMemoryModelService _modelService;

        private CancellationTokenSource tokenSource;
        private BlockingCollection<AudioSamples> _blockingCollection;
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

            if(File.Exists(Path.Combine(_hTFanControl._rootPath, "testaccuracy.txt")))
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
                Console.WriteLine("Stop Listening...");
                //Log.WriteLine("Stop Listening...");
            }

            _state = "";

            _modelService = null;
            _blockingCollection = null;
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
                if (File.Exists(Path.Combine(_hTFanControl._rootPath, "tmp", fileName + ".fingerprints")))
                {
                    validFilePath = Path.Combine(_hTFanControl._rootPath, "tmp", fileName + ".fingerprints");
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
            _blockingCollection = new BlockingCollection<AudioSamples>();

            Console.WriteLine("Start Listening...");
            //Log.WriteLine("Start Listening...");
            _state = "(listening...)";

            try
            {
                double queryResult = await GetBestMatchForStream(new BlockingRealtimeCollection<AudioSamples>(_blockingCollection), cancellationToken);
            }
            catch { }
        }

        private void FoundMatch(ResultEntry resultEntry)
        {
            _timeJump = false;
            TimeSpan matchTime = TimeSpan.FromSeconds(resultEntry.TrackMatchStartsAt + resultEntry.QueryLength - resultEntry.QueryMatchStartsAt + 0.65);

            if(matchTime > _lastMatchTime.Add(TimeSpan.FromMinutes(5)) || matchTime < _lastMatchTime.Subtract(TimeSpan.FromMinutes(5)))
            {
                _timeJump = true;
                _lastMatchTime = matchTime;
                Console.WriteLine("Time Jump Detected");
                //Log.WriteLine("Time Jump Detected");
            }

            if (!_timeJump)
            {
                Console.WriteLine($"Match Found: {matchTime:G}");
                //Log.WriteLine($"Match Found: {matchTime:G}");
                _hTFanControl._currentVideoTime = Convert.ToInt64(matchTime.TotalMilliseconds);
                _hTFanControl.UpdateTime();
                _lastMatchTime = matchTime;
            }

            //_pause.Change(10000, Timeout.Infinite);

            if (verifyAccuracy)
            {
                VerifyAccuracy(matchTime);
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

                position = long.Parse(doc.GetElementbyId("position").InnerText);
            }
            catch { }

            TimeSpan playerTime = TimeSpan.FromMilliseconds(position);
            string matchResult = $"Accuracy:{audioTime.Subtract(playerTime).TotalMilliseconds} AudioTime:{audioTime:G} PlayerTime:{playerTime:G}";
            Console.WriteLine(matchResult);
            //Log.WriteLine(matchResult);
        }

        //private void Pause(object o)
        //{
        //    _pause.Change(Timeout.Infinite, Timeout.Infinite);
        //    Console.WriteLine("PAUSED");
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

                            short[] samples = new short[samplesAvailable];
                            ALC.CaptureSamples(captureDevice, ref samples[0], samplesAvailable);

                            for (int i = 0; i < samples.Length; i += 2)
                            {
                                float fSample = samples[i] / 32768f;
                                _float32Buffer.Add(fSample);

                                if (_float32Buffer.Count == 10240)
                                {
                                    AudioSamples audioSamples = new AudioSamples(_float32Buffer.ToArray(), "", 5512);
                                    _blockingCollection.Add(audioSamples);

                                    _float32Buffer = new List<float>();
                                }
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

        private async Task<double> GetBestMatchForStream(BlockingRealtimeCollection<AudioSamples> audioSamples, CancellationToken token)
        {
            var queryResult = await QueryCommandBuilder.Instance
                                .BuildRealtimeQueryCommand()
                                .From(audioSamples)
                                .WithRealtimeQueryConfig(config =>
                                {
                                    config.ResultEntryFilter = new TrackMatchLengthEntryFilter(1d);
                                    config.SuccessCallback = FoundMatch;
                                    return config;
                                })
                                .UsingServices(_modelService)
                                .Query(token);
            return queryResult;
        }
    }
}