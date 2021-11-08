using HTFanControl.Players;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Xml.Linq;

namespace HTFanControl
{
    class PlexPlayer : IPlayer
    {
        private HttpClient _httpClient;

        private Settings _settings;

        public bool IsPlaying { get; private set; }
        public long VideoTime { get; private set; }
        public string FileName { get; private set; }
        public string FilePath { get; private set; }
        public string ErrorStatus { get; private set; }
        public int VideoTimeResolution { get; private set; }

        private string _pollingType = "1";

        public PlexPlayer(Settings settings)
        {
            VideoTimeResolution = 1000;

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
            try
            {
                using Stream timeStream = _httpClient.GetAsync($"http://{_settings.PlexClientIP}:{_settings.PlexClientPort}/player/timeline/poll?wait={_pollingType}&protocol=http&port=5501").Result.Content.ReadAsStreamAsync().Result;
                _pollingType = "0";
                XDocument timeXML = XDocument.Load(timeStream);
                XElement video = timeXML.Descendants("Timeline").Where(x => x.Attribute("type").Value == "video").First();

                VideoTime = long.Parse(video.Attribute("time").Value) + 500;

                string state = video.Attribute("state").Value;
                string fileKey = video.Attribute("ratingKey").Value;

                using Stream fileStream = _httpClient.GetAsync($"http://{_settings.MediaPlayerIP}:{_settings.MediaPlayerPort}/library/metadata/{fileKey}?X-Plex-Token={_settings.PlexToken}").Result.Content.ReadAsStreamAsync().Result;
                XDocument fileXML = XDocument.Load(fileStream);
                XElement media = fileXML.Descendants("MediaContainer").Descendants("Video").Descendants("Media").Descendants("Part").First();

                FileName = Path.GetFileNameWithoutExtension(media.Attribute("file").Value);
                FilePath = Path.GetDirectoryName(media.Attribute("file").Value);

                if (state == "playing")
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
                ErrorStatus = $"({DateTime.Now:h:mm:ss tt}) Cannot connect to Plex Player: {_settings.PlexClientName}";
                _pollingType = "1";
                return false;
            }

            return true;
        }
    }
}