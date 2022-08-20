using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Web;
using HTFanControl.Util;

namespace HTFanControl.Players
{
    class MPCPlayer : IPlayer
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

        public MPCPlayer(Settings settings)
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
                string html = _httpClient.GetStringAsync($"http://{_settings.MediaPlayerIP}:{_settings.MediaPlayerPort}/variables.html").Result;

                HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);

                FileName = Path.GetFileNameWithoutExtension(doc.GetElementbyId("file").InnerText);
                FilePath = doc.GetElementbyId("filedir").InnerText;

                VideoTime = long.Parse(doc.GetElementbyId("position").InnerText);

                //Get file from Kodi if MPC looks like it is a mounted ISO.
                if (FilePath.Contains(@"\BDMV"))
                {
                    GetFileFromKodi();
                }

                if (doc.GetElementbyId("statestring").InnerText == "Playing")
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
                ErrorStatus = $"({DateTime.Now:h:mm:ss tt}) Cannot connect to MPC at: {_settings.MediaPlayerIP}:{_settings.MediaPlayerPort}";
                return false;
            }

            return true;
        }

        private void GetFileFromKodi()
        {
            if (_playerID is null)
            {
                StringContent playerIDJSONRequest = new StringContent(@"{""jsonrpc"": ""2.0"", ""method"": ""Player.GetActivePlayers"", ""id"": 1}", System.Text.Encoding.UTF8, "application/json");
                string playerIDJSONResponse = _httpClient.PostAsync($"http://{_settings.MediaPlayerIP}:8080/jsonrpc", playerIDJSONRequest).Result.Content.ReadAsStringAsync().Result;

                using JsonDocument playerIdJSON = JsonDocument.Parse(playerIDJSONResponse);
                _playerID = playerIdJSON.RootElement.GetProperty("result")[0].GetProperty("playerid").GetRawText();
            }

            StringContent filenameJSONRequest = new StringContent(@"{""jsonrpc"": ""2.0"", ""method"": ""Player.GetItem"", ""params"": {""properties"": [""file""], ""playerid"": 1}, ""id"": " + _playerID + "}", System.Text.Encoding.UTF8, "application/json");

            HttpClient httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(1);

            string filenameJSONResponse = httpClient.PostAsync($"http://{_settings.MediaPlayerIP}:8080/jsonrpc", filenameJSONRequest).Result.Content.ReadAsStringAsync().Result;
            
            using JsonDocument fileInfoJSON = JsonDocument.Parse(filenameJSONResponse);
            string kodiFile = fileInfoJSON.RootElement.GetProperty("result").GetProperty("item").GetProperty("file").GetString();

            (string, string) fileInfo = ParseKodiFile(kodiFile);

            FileName = fileInfo.Item1;
            FilePath = fileInfo.Item2;
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