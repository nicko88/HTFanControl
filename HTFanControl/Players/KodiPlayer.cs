using HTFanControl.Players;
using System;
using System.IO;
using System.Text.Json;
using System.Web;

namespace HTFanControl
{
    class KodiPlayer : IPlayer
    {
        private string _IP;
        private string _port;

        public bool IsPlaying { get; private set; }
        public long VideoTime { get; private set; }
        public string FileName { get; private set; }
        public string FilePath { get; private set; }
        public string ErrorStatus { get; private set; }
        public int VideoTimeResolution { get; private set; }

        public KodiPlayer(string IP, string port)
        {
            VideoTimeResolution = 50;
            _IP = IP;
            _port = port;
        }

        public bool Update()
        {
            try
            {
                string timeJSONRequest = @"{""jsonrpc"": ""2.0"", ""method"": ""Player.GetProperties"", ""params"": {""properties"": [""time"", ""speed""], ""playerid"": 1}, ""id"": 1}";
                string filenameJSONRequest = @"{""jsonrpc"": ""2.0"", ""method"": ""Player.GetItem"", ""params"": {""properties"": [""file""], ""playerid"": 1}, ""id"": 1 }";

                using WebClientWithTimeout webClient = new WebClientWithTimeout();
                string timeJSONResponse = webClient.UploadString($"http://{_IP}:{_port}/jsonrpc", timeJSONRequest);
                string filenameJSONResponse = webClient.UploadString($"http://{_IP}:{_port}/jsonrpc", "POST", filenameJSONRequest);
                
                using JsonDocument time = JsonDocument.Parse(timeJSONResponse);
                long hours = time.RootElement.GetProperty("result").GetProperty("time").GetProperty("hours").GetInt64();
                long minutes = time.RootElement.GetProperty("result").GetProperty("time").GetProperty("minutes").GetInt64();
                long seconds = time.RootElement.GetProperty("result").GetProperty("time").GetProperty("seconds").GetInt64();
                long milliseconds = time.RootElement.GetProperty("result").GetProperty("time").GetProperty("milliseconds").GetInt64();

                VideoTime = (hours * 3600000) + (minutes * 60000) + (seconds * 1000) + milliseconds;

                using JsonDocument fileInfoJSON = JsonDocument.Parse(filenameJSONResponse);
                string filePath = fileInfoJSON.RootElement.GetProperty("result").GetProperty("item").GetProperty("file").GetString();

                (string, string) fileInfo = ParseKodiFile(filePath);
                FileName = fileInfo.Item1;
                FilePath = fileInfo.Item2;

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
            catch
            {
                ErrorStatus = $"({DateTime.Now:h:mm:ss tt}) Cannot connect to Kodi at: {_IP}:{_port}";
                return false;
            }

            return true;
        }


        private (string, string) ParseKodiFile(string filePathName)
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

                string revFilename = revInput.Substring(start, end - start);

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

        private string Reverse(string s)
        {
            char[] charArray = s.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }
    }
}