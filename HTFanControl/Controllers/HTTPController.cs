using HTFanControl.Util;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

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
            string httpURL = cmd switch
            {
                "OFF" => _settings.HTTP_OFF_URL,
                "ECO" => _settings.HTTP_ECO_URL,
                "LOW" => _settings.HTTP_LOW_URL,
                "MED" => _settings.HTTP_MED_URL,
                "HIGH" => _settings.HTTP_HIGH_URL,
                _ => _settings.HTTP_OFF_URL,
            };
            string httpPayload = cmd switch
            {
                "OFF" => _settings.HTTP_OFF_Payload,
                "ECO" => _settings.HTTP_ECO_Payload,
                "LOW" => _settings.HTTP_LOW_Payload,
                "MED" => _settings.HTTP_MED_Payload,
                "HIGH" => _settings.HTTP_HIGH_Payload,
                _ => _settings.HTTP_OFF_Payload,
            };

            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromMilliseconds(500);

                if (!string.IsNullOrEmpty(_settings.HTTP_Pass))
                {
                    if (string.IsNullOrEmpty(_settings.HTTP_User))
                    {
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.HTTP_Pass);
                    }
                    else
                    {
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes($"{_settings.HTTP_User}:{_settings.HTTP_Pass}")));
                    }
                }

                try
                {
                    StringContent postData = null;
                    if (!string.IsNullOrEmpty(httpPayload))
                    {
                        postData = new StringContent(httpPayload, Encoding.UTF8, "application/json");
                    }
                    if (!string.IsNullOrEmpty(httpURL))
                    {
                        HttpResponseMessage result = httpClient.PostAsync($"{httpURL}", postData).Result;

                        if (!result.IsSuccessStatusCode)
                        {
                            throw new Exception();
                        }
                    }
                }
                catch(Exception e)
                {
                    ErrorStatus = $"({DateTime.Now:h:mm:ss tt}) Failed sending http POST request to: {httpURL} with payload: {httpPayload}\n\n{e.Message}";
                    return false;
                }
            }

            return true;
        }

        public bool Connect() { return true; }

        public void Disconnect() { }
    }
}