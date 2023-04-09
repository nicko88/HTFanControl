using HTFanControl.Util;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace HTFanControl.Controllers
{
    class HTTPController : IController
    {
        private Settings _settings;

        public string ErrorStatus { get; private set; }

        public HTTPController(Settings settings)
        {
            _settings = settings;
        }

        public bool SendCMD(string cmd)
        {
            switch (cmd)
            {
                case "OFF":
                    SendHTTPPost(_settings.HTTP_OFF_URL);
                    SendHTTPPost(_settings.HTTP_OFF_URL2);
                    SendHTTPPost(_settings.HTTP_OFF_URL3);
                    SendHTTPPost(_settings.HTTP_OFF_URL4);
                    break;
                case "ECO":
                    SendHTTPPost(_settings.HTTP_ECO_URL);
                    SendHTTPPost(_settings.HTTP_ECO_URL2);
                    SendHTTPPost(_settings.HTTP_ECO_URL3);
                    SendHTTPPost(_settings.HTTP_ECO_URL4);
                    break;
                case "LOW":
                    SendHTTPPost(_settings.HTTP_LOW_URL);
                    SendHTTPPost(_settings.HTTP_LOW_URL2);
                    SendHTTPPost(_settings.HTTP_LOW_URL3);
                    SendHTTPPost(_settings.HTTP_LOW_URL4);
                    break;
                case "MED":
                    SendHTTPPost(_settings.HTTP_MED_URL);
                    SendHTTPPost(_settings.HTTP_MED_URL2);
                    SendHTTPPost(_settings.HTTP_MED_URL3);
                    SendHTTPPost(_settings.HTTP_MED_URL4);
                    break;
                case "HIGH":
                    SendHTTPPost(_settings.HTTP_HIGH_URL);
                    SendHTTPPost(_settings.HTTP_HIGH_URL2);
                    SendHTTPPost(_settings.HTTP_HIGH_URL3);
                    SendHTTPPost(_settings.HTTP_HIGH_URL4);
                    break;
                default:
                    SendHTTPPost(_settings.HTTP_OFF_URL);
                    SendHTTPPost(_settings.HTTP_OFF_URL2);
                    SendHTTPPost(_settings.HTTP_OFF_URL3);
                    SendHTTPPost(_settings.HTTP_OFF_URL4);
                    break;
            }

            return true;
        }

        private void SendHTTPPost(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                Task.Run(() =>
                {
                    using (HttpClient httpClient = new HttpClient())
                    {
                        httpClient.Timeout = TimeSpan.FromMilliseconds(500);

                        try
                        {
                            StringContent postData = null;

                            _ = httpClient.PostAsync($"{url}", postData).Result;
                        }
                        catch { }
                    }
                });
            }
        }

        public bool Connect() { return true; }

        public void Disconnect() { }
    }
}