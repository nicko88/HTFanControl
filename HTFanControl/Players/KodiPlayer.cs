using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Web;
using HTFanControl.Util;

namespace HTFanControl.Players
{
    class KodiPlayer : IPlayer
    {
        private HttpClient _httpClient;
        private Settings _settings;
        private string _playerID = null;

        public bool IsPlaying { get; private set; }
        public long VideoTime { get; private set; }
        public string FileName { get; private set; }
        public string FilePath { get; private set; }
        public string ErrorStatus { get; private set; }
        public int VideoTimeResolution { get; private set; }

        public KodiPlayer(Settings settings)
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
                if(_playerID is null)
                {
                    StringContent playerIDJSONRequest = new StringContent(@"{""jsonrpc"": ""2.0"", ""method"": ""Player.GetActivePlayers"", ""id"": 1}", System.Text.Encoding.UTF8, "application/json");
                    string playerIDJSONResponse = _httpClient.PostAsync($"http://{_settings.MediaPlayerIP}:{_settings.MediaPlayerPort}/jsonrpc", playerIDJSONRequest).Result.Content.ReadAsStringAsync().Result;

                    using JsonDocument playerIdJSON = JsonDocument.Parse(playerIDJSONResponse);
                    _playerID = playerIdJSON.RootElement.GetProperty("result")[0].GetProperty("playerid").GetRawText();
                }

                StringContent filenameJSONRequest = new StringContent(@"{""jsonrpc"": ""2.0"", ""method"": ""Player.GetItem"", ""params"": {""properties"": [""file""], ""playerid"": 1}, ""id"": " + "1" + "}", System.Text.Encoding.UTF8, "application/json");
                string filenameJSONResponse = _httpClient.PostAsync($"http://{_settings.MediaPlayerIP}:{_settings.MediaPlayerPort}/jsonrpc", filenameJSONRequest).Result.Content.ReadAsStringAsync().Result;

                using JsonDocument fileInfoJSON = JsonDocument.Parse(filenameJSONResponse);
                string filePath = fileInfoJSON.RootElement.GetProperty("result").GetProperty("item").GetProperty("file").GetString();

                (string, string) fileInfo = ParseKodiFile(filePath);
                FileName = fileInfo.Item1;
                FilePath = fileInfo.Item2;

                //for PlexKodiConnect plugin
                if (FileName.Contains("plex_id"))
                {
                    FileName = FileName.Substring(FileName.LastIndexOf("&filename=") + 10);
                }

                //for Plex for Kodi plugin
                if(filePath.Contains("plex.direct"))
                {
                    if(filePath.Contains("/transcode/"))
                    {
                        FileName = "Video being transcoded by Plex, only Direct Play (Original quality) supported";
                    }
                    else
                    {
                        FileName = new string(fileInfoJSON.RootElement.GetProperty("result").GetProperty("item").GetProperty("label").GetString().Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)).ToArray());
                    }
                }

                bool getKodiTime = true;

                if (_settings.MediaPlayerType == "KodiMPC")
                {
                    try
                    {
                        string html = _httpClient.GetStringAsync($"http://{_settings.MediaPlayerIP}:13579/variables.html").Result;

                        HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
                        doc.LoadHtml(html);

                        VideoTime = long.Parse(doc.GetElementbyId("position").InnerText);

                        if (doc.GetElementbyId("statestring").InnerText == "Playing")
                        {
                            IsPlaying = true;
                        }
                        else
                        {
                            IsPlaying = false;
                        }

                        getKodiTime = false;
                    }
                    catch { }
                }

                if (getKodiTime)
                {
                    StringContent timeJSONRequest = new StringContent(@"{""jsonrpc"": ""2.0"", ""method"": ""Player.GetProperties"", ""params"": {""properties"": [""time"", ""speed""], ""playerid"": 1}, ""id"": " + _playerID + "}", System.Text.Encoding.UTF8, "application/json");
                    string timeJSONResponse = _httpClient.PostAsync($"http://{_settings.MediaPlayerIP}:{_settings.MediaPlayerPort}/jsonrpc", timeJSONRequest).Result.Content.ReadAsStringAsync().Result;

                    using JsonDocument time = JsonDocument.Parse(timeJSONResponse);
                    long hours = time.RootElement.GetProperty("result").GetProperty("time").GetProperty("hours").GetInt64();
                    long minutes = time.RootElement.GetProperty("result").GetProperty("time").GetProperty("minutes").GetInt64();
                    long seconds = time.RootElement.GetProperty("result").GetProperty("time").GetProperty("seconds").GetInt64();
                    long milliseconds = time.RootElement.GetProperty("result").GetProperty("time").GetProperty("milliseconds").GetInt64();

                    VideoTime = (hours * 3600000) + (minutes * 60000) + (seconds * 1000) + milliseconds + 200;

                    using JsonDocument state = JsonDocument.Parse(timeJSONResponse);
                    int stateNum = state.RootElement.GetProperty("result").GetProperty("speed").GetInt32();

                    if (stateNum == 1)
                    {
                        IsPlaying = true;
                    }
                    else
                    {
                        IsPlaying = false;
                    }
                }
            }
            catch
            {
                ErrorStatus = $"({DateTime.Now:h:mm:ss tt}) Cannot connect to Kodi at: {_settings.MediaPlayerIP}:{_settings.MediaPlayerPort}";
                return false;
            }

            return true;
        }


        private static (string, string) ParseKodiFile(string filePathName)
        {
            string fileName;
            string decodedInput = HttpUtility.UrlDecode(HttpUtility.UrlDecode(filePathName));

            if (filePathName.Contains("bluray:"))
            {
                string revInput = Reverse(decodedInput);

                int start = revInput.IndexOf("osi.") + 4;

                int end = revInput.IndexOf(@"\", start);
                if (end == -1)
                {
                    end = revInput.IndexOf(@"/", start);
                }

                string revFilename = revInput[start..end];

                fileName = Reverse(revFilename);
            }
            else
            {
                fileName = Path.GetFileNameWithoutExtension(filePathName);
            }

            string filePath;
            if (filePathName.Contains("bluray:"))
            {
                decodedInput = decodedInput.Replace(@"bluray://", "");
                decodedInput = decodedInput.Replace(@"udf://", "");
                decodedInput = decodedInput.Replace(@"smb:", "");

                int end = decodedInput.IndexOf(fileName);

                filePath = decodedInput.Substring(0, end);
            }
            else
            {
                filePath = filePathName.Replace("smb:", "");
                filePath = Path.GetDirectoryName(filePath);
            }

            return (fileName, filePath);
        }

        private static string Reverse(string s)
        {
            char[] charArray = s.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }
    }
}