using HTFanControl.Util;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;

namespace HTFanControl.Players
{
    class ZidooPlayer : IPlayer
    {
        private HttpClient _httpClient;
        private Settings _settings;

        public bool IsPlaying { get; private set; }
        public long VideoTime { get; private set; }
        public string FileName { get; private set; }
        public string FilePath { get; private set; }
        public string ErrorStatus { get; private set; }
        public int VideoTimeResolution { get; private set; }

        public ZidooPlayer(Settings settings)
        {
            VideoTimeResolution = 50;

            _settings = settings;

            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(1);
        }

        public bool Update()
        {
            try
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();

                string playerStatusJSONResponse = _httpClient.GetStringAsync($"http://{_settings.MediaPlayerIP}:{_settings.MediaPlayerPort}/ZidooVideoPlay/getPlayStatus").Result;
                using JsonDocument playerstatusJSON = JsonDocument.Parse(playerStatusJSONResponse);

                FileName = new string(playerstatusJSON.RootElement.GetProperty("video").GetProperty("title").GetString().Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)).ToArray());
                VideoTime = playerstatusJSON.RootElement.GetProperty("video").GetProperty("currentPosition").GetInt32() + sw.ElapsedMilliseconds;
                sw.Stop();

                int playState = playerstatusJSON.RootElement.GetProperty("video").GetProperty("status").GetInt32();

                if (playState == 1)
                {
                    IsPlaying = true;
                }
                else if (playState == 0)
                {
                    IsPlaying = false;
                }
            }
            catch
            {
                ErrorStatus = $"({DateTime.Now:h:mm:ss tt}) Cannot connect to Zidoo at: {_settings.MediaPlayerIP}:{_settings.MediaPlayerPort}";
                return false;
            }

            return true;
        }
    }
}