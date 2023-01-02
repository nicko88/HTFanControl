using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HTFanControl.Util;

namespace HTFanControl.Players
{
    class RokuPlexPlayer : IPlayer
    {
        private HttpClient _httpClient;
        private Settings _settings;
        private string _pollingType = "1";

        public bool IsPlaying { get; private set; }
        public long VideoTime { get; private set; }
        public string FileName { get; private set; }
        public string FilePath { get; private set; }
        public string ErrorStatus { get; private set; }
        public int VideoTimeResolution { get; private set; }

        public RokuPlexPlayer(Settings settings)
        {
            VideoTimeResolution = 50;

            _settings = settings;

            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
            _httpClient.DefaultRequestHeaders.Add("X-Plex-Client-Identifier", "HTFanControl");
            _httpClient.DefaultRequestHeaders.Add("X-Plex-Device-Name", "HTFanControl");
            _httpClient.DefaultRequestHeaders.Add("X-Plex-Target-Client-Identifier", _settings.PlexClientGUID);
        }

        public bool Update()
        {
            string fileKey = "";

            try
            {
                using Stream timeStream = _httpClient.GetAsync($"http://{_settings.PlexClientIP}:{_settings.PlexClientPort}/player/timeline/poll?wait={_pollingType}&protocol=http&port=5501").Result.Content.ReadAsStreamAsync().Result;
                _pollingType = "0";
                XDocument timeXML = XDocument.Load(timeStream);
                XElement video = timeXML.Descendants("Timeline").Where(x => x.Attribute("type").Value == "video").First();
                fileKey = video.Attribute("ratingKey").Value;
            }
            catch
            {
                ErrorStatus = $"({DateTime.Now:h:mm:ss tt}) Cannot connect to Roku Plex App: {_settings.PlexClientName} ({_settings.PlexClientIP}:{_settings.PlexClientPort})";
                _pollingType = "1";
                return false;
            }

            try
            {
                using Stream fileStream = _httpClient.GetAsync($"http://{_settings.MediaPlayerIP}:{_settings.MediaPlayerPort}/library/metadata/{fileKey}?X-Plex-Token={_settings.PlexToken}").Result.Content.ReadAsStreamAsync().Result;
                XDocument fileXML = XDocument.Load(fileStream);
                XElement media = fileXML.Descendants("MediaContainer").Descendants("Video").Descendants("Media").Descendants("Part").First();

                FileName = Path.GetFileNameWithoutExtension(media.Attribute("file").Value);
                FilePath = Path.GetDirectoryName(media.Attribute("file").Value);

            }
            catch
            {
                ErrorStatus = $"({DateTime.Now:h:mm:ss tt}) Cannot connect to Plex Media Server at: {_settings.MediaPlayerIP}:{_settings.MediaPlayerPort}";
                _pollingType = "1";
                return false;
            }

            try
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();

                using Stream rokuStream = _httpClient.GetAsync($"http://{_settings.PlexClientIP}:8060/query/media-player").Result.Content.ReadAsStreamAsync().Result;
                XDocument rokuXML = XDocument.Load(rokuStream);
                string strPosition = rokuXML.Descendants().First().Descendants("position").First().Value;

                //Roku API seems to be somewhat slow and a bit inconsistent, so we add some time to the returned time to compensate.
                VideoTime = Convert.ToInt64(Regex.Replace(strPosition, "[^0-9.]", "")) + 250 + sw.ElapsedMilliseconds;
                sw.Stop();

                string state = rokuXML.Root.Attribute("state").Value;

                if (state == "play")
                {
                    IsPlaying = true;
                }
                else
                {
                    IsPlaying = false;
                }
            }
            catch
            {
                ErrorStatus = $"({DateTime.Now:h:mm:ss tt}) Cannot connect to Roku Media Player: {_settings.PlexClientName} ({_settings.PlexClientIP}:8060)";
                return false;
            }

            return true;
        }
    }
}