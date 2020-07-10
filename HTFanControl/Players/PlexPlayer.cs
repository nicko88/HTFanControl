using HTFanControl.Players;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.Linq;

namespace HTFanControl
{
    class PlexPlayer : IPlayer
    {
        private string _serverIP;
        private string _serverPort;
        private string _serverToken;
        private string _playerIP;
        private string _playerPort;
        private string _playerGUID;
        private string _playerName;

        public bool IsPlaying { get; private set; }
        public long VideoTime { get; private set; }
        public string FileName { get; private set; }
        public string FilePath { get; private set; }
        public string ErrorStatus { get; private set; }
        public int RefreshInterval { get; private set; }

        public PlexPlayer(string serverIP, string serverPort, string serverToken, string playerIP, string playerPort, string playerGUID, string playerName)
        {
            RefreshInterval = 1000;

            _serverIP = serverIP;
            _serverPort = serverPort;
            _serverToken = serverToken;
            _playerIP = playerIP;
            _playerPort = playerPort;
            _playerGUID = playerGUID;
            _playerName = playerName;
        }

        public bool Update()
        {
            try
            {
                WebRequest timeRequest = WebRequest.Create($"http://{_playerIP}:{_playerPort}/player/timeline/poll?wait=0&protocol=http&port=5501&commandID=1");
                timeRequest.Method = "GET";
                timeRequest.Timeout = 10000;
                timeRequest.ContentType = "application/xml";
                timeRequest.Headers.Add("X-Plex-Client-Identifier", "HTFanControl");
                timeRequest.Headers.Add("X-Plex-Device-Name", "HTFanControl");
                timeRequest.Headers.Add("X-Plex-Target-Client-Identifier", _playerGUID);

                using Stream timeStream = timeRequest.GetResponse().GetResponseStream();
                XDocument timeXML = XDocument.Load(timeStream);
                XElement video = timeXML.Descendants("Timeline").Where(x => x.Attribute("type").Value == "video").First();

                VideoTime = Int64.Parse(video.Attribute("time").Value);

                string state = video.Attribute("state").Value;
                string fileKey = video.Attribute("ratingKey").Value;

                WebRequest fileRequest = WebRequest.Create($"http://{_serverIP}:{_serverPort}/library/metadata/{fileKey}?X-Plex-Token={_serverToken}");
                fileRequest.Method = "GET";
                fileRequest.Timeout = 10000;
                fileRequest.ContentType = "application/xml";

                using Stream fileStream = fileRequest.GetResponse().GetResponseStream();
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
                ErrorStatus = $"({DateTime.Now:h:mm:ss tt}) Cannot connect to Plex Player: {_playerName}";
                return false;
            }

            return true;
        }
    }
}