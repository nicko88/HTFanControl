using HTFanControl.Players;
using System;
using System.IO;
using System.Text.Json;
using System.Web;

namespace HTFanControl
{
    class MPCPlayer : IPlayer
    {
        private string _IP;
        private string _port;

        public bool IsPlaying { get; private set; }
        public long VideoTime { get; private set; }
        public string FileName { get; private set; }
        public string FilePath { get; private set; }
        public string ErrorStatus { get; private set; }
        public int RefreshInterval { get; private set; }

        public MPCPlayer(string IP, string port)
        {
            RefreshInterval = 1000;
            _IP = IP;
            _port = port;
        }

        public bool Update()
        {
            try
            {
                using WebClientWithTimeout client = new WebClientWithTimeout();
                string html = client.DownloadString($"http://{_IP}:{_port}/variables.html");

                HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);

                VideoTime = Int64.Parse(doc.GetElementbyId("position").InnerText);

                FileName = Path.GetFileNameWithoutExtension(doc.GetElementbyId("file").InnerText);
                FilePath = doc.GetElementbyId("filedir").InnerText;

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
                ErrorStatus = $"({DateTime.Now:h:mm:ss tt}) Cannot connect to MPC at: {_IP}:{_port}";
                return false;
            }

            return true;
        }

        private void GetFileFromKodi()
        {
            string filenameJSONRequest = @"{""jsonrpc"": ""2.0"", ""method"": ""Player.GetItem"", ""params"": {""properties"": [""file""], ""playerid"": 1}, ""id"": 1 }";

            using WebClientWithTimeout webClient = new WebClientWithTimeout();
            string filenameJSONResponse = webClient.UploadString($"http://{_IP}:8080/jsonrpc", "POST", filenameJSONRequest);
            
            using JsonDocument fileInfoJSON = JsonDocument.Parse(filenameJSONResponse);
            string kodiFile = fileInfoJSON.RootElement.GetProperty("result").GetProperty("item").GetProperty("file").GetString();

            (string, string) fileInfo = ParseKodiFile(kodiFile);

            FileName = fileInfo.Item1;
            FilePath = fileInfo.Item2;
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