using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Text.Json;
using System.Collections.Generic;
using System.Web;
using System.Diagnostics;
using System.Xml.Linq;
using System.Net.Http;
using System.Net.Http.Headers;

namespace HTFanControl
{
    class WebUI
    {
        private readonly string _version = "Beta19";
        private readonly Thread _httpThread;
        private readonly HTFanControl _HTFanCtrl;
        private bool _waitForFile = false;

        public WebUI()
        {
            _httpThread = new Thread(StartListen);
            _httpThread.Start();

            _HTFanCtrl = new HTFanControl();
        }

        private void StartListen()
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://*:5500/");
            listener.Start();

            while (true)
            {
                IAsyncResult result = listener.BeginGetContext(new AsyncCallback(ProcessRequest), listener);
                result.AsyncWaitHandle.WaitOne();
            }
        }

        private void ProcessRequest(IAsyncResult result)
        {
            if (_HTFanCtrl != null)
            {
                HttpListener listener = (HttpListener)result.AsyncState;
                HttpListenerContext context = listener.EndGetContext(result);
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                string htmlResponse = "";
                switch (request.RawUrl)
                {
                    case "/":
                        htmlResponse = StatusPage(request, "status");
                        break;
                    case "/statusdata":
                        htmlResponse = GetStatusData(request, "statusdata");
                        break;
                    case "/currentmovie":
                        htmlResponse = GetCurrentMovie(request, "currentmovie");
                        break;
                    case "/settings":
                        htmlResponse = SettingsPage(request, "settings");
                        break;
                    case "/savesettings":
                        SaveSettings(request, "savesettings");
                        break;
                    case "/raspiwifi":
                        htmlResponse = RasPiWiFiPage(request, "raspiwifi");
                        break;
                    case "/savewifi":
                        SaveWiFi(request, "savewifi");
                        break;
                    case "/downloadlist":
                        htmlResponse = DownloadListPage(request, "downloadlist");
                        break;
                    case "/download":
                        htmlResponse = DownloadPage(request, "download");
                        break;
                    case "/manage":
                        htmlResponse = ManagePage(request, "manage");
                        break;
                    case "/edit":
                        htmlResponse = EditPage(request, "edit");
                        break;
                    case "/add":
                        htmlResponse = GetHtml("add");
                        break;
                    case "/uploadlocal":
                        _waitForFile = true;
                        UploadLocal(request, "uploadlocal");
                        break;
                    case "/rename":
                        RenameFile(request, "rename");
                        break;
                    case "/delete":
                        _waitForFile = true;
                        DeleteFile(request, "delete");
                        break;
                    case "/save":
                        SaveFile(request, "save");
                        break;
                    case "/selectplexplayer":
                        htmlResponse = GetHtml("selectplexplayer");
                        break;
                    case "/getplexplayers":
                        htmlResponse = GetPlexPlayer(request, "getplexplayers");
                        break;
                    case "/saveplexplayer":
                        _waitForFile = true;
                        SavePlexPlayer(request, "saveplexplayer");
                        break;
                    case "/reload":
                        _HTFanCtrl.ReInitialize(true);
                        break;
                    case "/togglefan":
                        _HTFanCtrl.ToggleFan();
                        break;
                    case "/toggleoffset":
                        _HTFanCtrl._offsetEnabled = !_HTFanCtrl._offsetEnabled;
                        _HTFanCtrl.ReInitialize(true);
                        break;
                    case "/fantester":
                        _HTFanCtrl.ReInitialize(true);
                        htmlResponse = GetHtml("fantester");
                        break;
                    case "/loadedwindtrack":
                        htmlResponse = GetHtml("loadedwindtrack");
                        break;
                    case "/loadedwindtrackdata":
                        htmlResponse = LoadedWindTrackData(request, "loadedwindtrackdata");
                        break;
                    case "/fancmd":
                        FanCmd(request, "fancmd");
                        break;
                    case "/clearerror":
                        _HTFanCtrl._errorStatus = null;
                        break;
                    case "/checkupdate":
                        htmlResponse = CheckUpdatePage(request, "checkupdate");
                        break;
                    case "/raspiupdate":
                        ($"nohup {Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "updater.sh")} &>/dev/null &").Bash();
                        Environment.Exit(0);
                        break;
                    case "/shutdown":
                        Environment.Exit(0);
                        break;
                    default:
                        break;
                }

                byte[] buffer = Encoding.ASCII.GetBytes(htmlResponse);
                response.ContentLength64 = buffer.Length;
                Stream output = response.OutputStream;
                try
                {
                    output.Write(buffer, 0, buffer.Length);
                    output.Close();
                }
                catch { }
            }
        }

        private string StatusPage(HttpListenerRequest request, string pageName)
        {
            string html = GetHtml(pageName);

            if (_HTFanCtrl._isEnabled)
            {
                html = html.Replace("{color}", "success");
                html = html.Replace("{status}", "Fans Enabled");
            }
            else
            {
                html = html.Replace("{color}", "danger");
                html = html.Replace("{status}", "Fans Disabled");
            }

            html = html.Replace("{version}", _version);

            return html;
        }

        private string GetStatusData(HttpListenerRequest request, string pageName)
        {
            StringBuilder htmlData = new StringBuilder();

            if (_HTFanCtrl != null)
            {
                htmlData.AppendLine(GetCurrentMovie(request, pageName));
                if (_HTFanCtrl._videoTimeCodes != null)
                {
                    htmlData.AppendLine("<br /><br />");
                    htmlData.AppendLine("<b>Wind track info:</b>");
                    htmlData.AppendLine("<br />");
                    htmlData.AppendLine(_HTFanCtrl._windTrackHeader);
                    htmlData.AppendLine("Codes loaded: " + _HTFanCtrl._videoTimeCodes.Count);

                    htmlData.AppendLine("<br />");
                    htmlData.AppendLine("<button onclick=\"window.location.href = 'loadedwindtrack';\" class=\"btn btn-primary\">View Wind Track</button>");

                    if (_HTFanCtrl._offset != 0)
                    {
                        if (_HTFanCtrl._offsetEnabled)
                        {
                            htmlData.AppendLine("<button onclick=\"toggleOffset()\" class=\"btn btn-success\">Offset Enabled</button>");
                        }
                        else
                        {
                            htmlData.AppendLine("<button onclick=\"toggleOffset()\" class=\"btn btn-danger\">Offset Disabled</button>");
                        }
                    }
                }

                htmlData.AppendLine("<br /><br />");
                htmlData.Append("Current time: " + TimeSpan.FromMilliseconds(_HTFanCtrl._currentVideoTime).ToString("G").Substring(2, 12));
                htmlData.AppendLine("<br /><br />");

                if (_HTFanCtrl._videoTimeCodes != null)
                {
                    htmlData.AppendLine("<b>Current Command:</b>");
                    htmlData.AppendLine("<br />");
                    if (_HTFanCtrl._curCmdIndex == -1)
                    {
                        htmlData.AppendLine("OFF");
                    }
                    else
                    {
                        if (_HTFanCtrl._curCmdIndex > -1 && _HTFanCtrl._curCmdIndex < _HTFanCtrl._videoTimeCodes.Count)
                        {
                            htmlData.AppendLine(_HTFanCtrl._videoTimeCodes[_HTFanCtrl._curCmdIndex].Item1.ToString("G").Substring(2, 12) + "," + _HTFanCtrl._videoTimeCodes[_HTFanCtrl._curCmdIndex].Item2);
                        }
                    }

                    htmlData.AppendLine("<br /><br />");
                    htmlData.AppendLine("<b>Next Command:</b>");
                    htmlData.AppendLine("<br />");
                    if (_HTFanCtrl._nextCmdIndex > -1 && _HTFanCtrl._nextCmdIndex < _HTFanCtrl._videoTimeCodes.Count)
                    {
                        htmlData.AppendLine(_HTFanCtrl._videoTimeCodes[_HTFanCtrl._nextCmdIndex].Item1.ToString("G").Substring(2, 12) + "," + _HTFanCtrl._videoTimeCodes[_HTFanCtrl._nextCmdIndex].Item2);
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(_HTFanCtrl._currentVideoFileName))
                    {
                        htmlData.AppendLine("No wind track file found named: " + _HTFanCtrl._currentVideoFileName + ".txt");
                        htmlData.AppendLine("<br /><br />");
                    }
                    htmlData.AppendLine("<b>Current Command:</b>");
                    htmlData.AppendLine("<br />");
                    htmlData.AppendLine("OFF");
                }

                if (_HTFanCtrl._errorStatus != null)
                {
                    htmlData.AppendLine("<br /><br />");
                    htmlData.AppendLine("Last error: " + _HTFanCtrl._errorStatus);
                    htmlData.AppendLine("<a href='#' onclick='clearError()'>(clear)</a>");
                }
                if (_HTFanCtrl._windtrackError != null)
                {
                    htmlData.AppendLine("<br /><br />");
                    htmlData.AppendLine("Windtrack error: " + _HTFanCtrl._windtrackError);
                }
                htmlData.AppendLine("<br /><br />");
            }
            return htmlData.ToString();
        }

        private string SettingsPage(HttpListenerRequest request, string pageName)
        {
            if (_waitForFile)
            {
                Thread.Sleep(250);
                _waitForFile = false;
            }

            string html;
            if (ConfigHelper.GetOS() == "win")
            {
                html = GetHtml("settings");
            }
            else
            {
                html = GetHtml("settingslinux");
            }

            html = html.Replace("{LircIP}", _HTFanCtrl._lircIP);
            html = html.Replace("{LircPort}", _HTFanCtrl._lircPort);
            html = html.Replace("{LircRemote}", _HTFanCtrl._lircRemote);

            if (_HTFanCtrl._mediaPlayerType == "MPC")
            {
                html = html.Replace("{MPC}", "checked");
                html = html.Replace("{Kodi}", "");
                html = html.Replace("{Plex}", "");
                html = html.Replace("{lblPlayer}", "MPC-HC/BE IP");
            }
            else if (_HTFanCtrl._mediaPlayerType.Contains("Kodi"))
            {
                html = html.Replace("{MPC}", "");
                html = html.Replace("{Kodi}", "checked");
                html = html.Replace("{Plex}", "");
                html = html.Replace("{lblPlayer}", "Kodi IP");
            }
            else
            {
                html = html.Replace("{MPC}", "");
                html = html.Replace("{Kodi}", "");
                html = html.Replace("{Plex}", "checked");
                html = html.Replace("{lblPlayer}", "Plex Media Server IP");
            }

            html = html.Replace("{MediaPlayerIP}", _HTFanCtrl._mediaPlayerIP);
            html = html.Replace("{MediaPlayerPort}", _HTFanCtrl._mediaPlayerPort);

            html = html.Replace("{PlexToken}", _HTFanCtrl._PlexToken);

            if (string.IsNullOrEmpty(_HTFanCtrl._plexClientName))
            {
                html = html.Replace("{PlexPlayer}", "None");
            }
            else
            {
                html = html.Replace("{PlexPlayer}", $"{_HTFanCtrl._plexClientName} ({_HTFanCtrl._plexClientIP})");
            }

            html = html.Replace("{GlobalOffset}", _HTFanCtrl._globalOffsetMS.ToString());
            html = html.Replace("{SpinupOffset}", _HTFanCtrl._spinupOffsetMS.ToString());
            html = html.Replace("{SpindownOffset}", _HTFanCtrl._spindownOffsetMS.ToString());

            if (ConfigHelper.GetOS() != "win")
            {
                if(_HTFanCtrl._irChan1 == "true")
                {
                    html = html.Replace("{IRChan1}", "checked");
                }
                else
                {
                    html = html.Replace("{IRChan1}", "");
                }
                if (_HTFanCtrl._irChan2 == "true")
                {
                    html = html.Replace("{IRChan2}", "checked");
                }
                else
                {
                    html = html.Replace("{IRChan2}", "");
                }
                if (_HTFanCtrl._irChan3 == "true")
                {
                    html = html.Replace("{IRChan3}", "checked");
                }
                else
                {
                    html = html.Replace("{IRChan3}", "");
                }
                if (_HTFanCtrl._irChan4 == "true")
                {
                    html = html.Replace("{IRChan4}", "checked");
                }
                else
                {
                    html = html.Replace("{IRChan4}", "");
                }
            }

            html = html.Replace("{version}", @$"Version: {_version} <a href=""checkupdate"">(Check For Update)</a>");

            return html;
        }

        private void SaveSettings(HttpListenerRequest request, string pageName)
        {
            string settingsInfoJSON = GetPostBody(request);

            if (!string.IsNullOrEmpty(settingsInfoJSON))
            {
                using JsonDocument data = JsonDocument.Parse(settingsInfoJSON);

                _HTFanCtrl._lircIP = data.RootElement.GetProperty("LircIP").GetString();
                _HTFanCtrl._lircPort = data.RootElement.GetProperty("LircPort").GetString();
                _HTFanCtrl._lircRemote = data.RootElement.GetProperty("LircRemote").GetString();
                _HTFanCtrl._mediaPlayerIP = data.RootElement.GetProperty("MediaPlayerIP").GetString();
                _HTFanCtrl._mediaPlayerPort = data.RootElement.GetProperty("MediaPlayerPort").GetString();
                _HTFanCtrl._globalOffsetMS = data.RootElement.GetProperty("GlobalOffset").GetString();
                _HTFanCtrl._spinupOffsetMS = data.RootElement.GetProperty("SpinupOffset").GetString();
                _HTFanCtrl._spindownOffsetMS = data.RootElement.GetProperty("SpindownOffset").GetString();
                _HTFanCtrl._mediaPlayerType = data.RootElement.GetProperty("MediaPlayer").GetString();
                _HTFanCtrl._PlexToken = data.RootElement.GetProperty("PlexToken").GetString();

                if (ConfigHelper.GetOS() != "win")
                {
                    _HTFanCtrl._irChan1 = data.RootElement.GetProperty("IRChan1").GetBoolean().ToString().ToLower();
                    _HTFanCtrl._irChan2 = data.RootElement.GetProperty("IRChan2").GetBoolean().ToString().ToLower();
                    _HTFanCtrl._irChan3 = data.RootElement.GetProperty("IRChan3").GetBoolean().ToString().ToLower();
                    _HTFanCtrl._irChan4 = data.RootElement.GetProperty("IRChan4").GetBoolean().ToString().ToLower();
                }

                _HTFanCtrl.SaveSettings();
                _HTFanCtrl.ReInitialize(true);
            }
        }

        private static string RasPiWiFiPage(HttpListenerRequest request, string pageName)
        {
            string html = GetHtml(pageName);
            try
            {
                string netplan = ("cat /etc/netplan/50-cloud-init.yaml").Bash();

                int ssidStart = netplan.IndexOf("access-points:") + 30;
                int ssidEnd = netplan.IndexOf("password:") - 19;
                string ssid = netplan[ssidStart..ssidEnd];

                int passwordStart = netplan.IndexOf("password:") + 10;
                int passwordEnd = netplan.Length - 1;
                string password = netplan[passwordStart..passwordEnd];

                html = html.Replace("{ssid}", ssid);
                html = html.Replace("{password}", password);
            }
            catch { }

            return html;
        }

        private static void SaveWiFi(HttpListenerRequest request, string pageName)
        {
            string wifiInfoJSON = GetPostBody(request);

            if (!string.IsNullOrEmpty(wifiInfoJSON))
            {
                string netplan = "";
                try
                {
                    Assembly assembly = Assembly.GetExecutingAssembly();
                    string resourceName = assembly.GetManifestResourceNames().Single(str => str.EndsWith("50-cloud-init.yaml"));

                    using Stream stream = assembly.GetManifestResourceStream(resourceName);
                    using StreamReader reader = new StreamReader(stream);
                    netplan = reader.ReadToEnd();
                }
                catch { }

                using JsonDocument data = JsonDocument.Parse(wifiInfoJSON);
                string ssid = data.RootElement.GetProperty("ssid").GetString();
                string password = data.RootElement.GetProperty("password").GetString();

                netplan = netplan.Replace("{ssid}", ssid);
                netplan = netplan.Replace("{pass}", password);

                ($"echo \"{netplan}\" > /etc/netplan/50-cloud-init.yaml").Bash();
                "netplan apply".Bash();
            }
        }

        private string CheckUpdatePage(HttpListenerRequest request, string pageName)
        {
            string html = GetHtml(pageName);

            StringBuilder sb = new StringBuilder();
            try
            {
                sb.AppendLine("</br>");
                sb.AppendLine($"<b>Current Version:</b> {_version}");
                sb.AppendLine("</br>");

                HttpClient httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", ".netapp");
                string releaseJSON = httpClient.GetAsync("https://api.github.com/repos/nicko88/htfancontrol/releases/latest").Result.Content.ReadAsStringAsync().Result;
                using JsonDocument data = JsonDocument.Parse(releaseJSON);
                string latest = data.RootElement.GetProperty("tag_name").GetString();
                string latesturl = data.RootElement.GetProperty("html_url").GetString();

                sb.AppendLine($"<b>Latest Version:</b> {latest}");
                sb.AppendLine("</br></br>");

                if (_version != latest)
                {
                    if (ConfigHelper.GetOS() == "raspi")
                    {
                        sb.AppendLine(@"<button id=""btnupdate"" onclick=""raspiupdate();"" class=""btn btn-primary"">Update HTFanControl</button>");
                    }
                    else
                    {
                        sb.AppendLine(@$"<a href=""{latesturl}""><b>Get Latest Version Here</b></a>");
                    }
                }
                else
                {
                    sb.AppendLine(@$"<b>You Have The Latest Version</b>");
                }

                html = html.Replace("{body}", sb.ToString());
            }
            catch { }

            return html;
        }

        private static string DownloadListPage(HttpListenerRequest request, string pageName)
        {
            string html = GetHtml(pageName);

            HttpClient httpClient = new HttpClient();
            try
            {
                string fileIndex = httpClient.GetStringAsync("https://pastebin.com/raw/uWMR92bf").Result;

                string[] lines = fileIndex.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                StringBuilder sb = new StringBuilder();
                foreach (string s in lines)
                {
                    string[] values = s.Split('=');
                    sb.AppendFormat(@"<span style=""padding: 4px 8px;"" onclick=""downloadfile('https://pastebin.com/raw/{0}', '{1}')"" class=""list-group-item list-group-item-action list-group-item-dark"">{1}</span>" + "\n", values[1], values[0]);
                }

                html = html.Replace("{body}", sb.ToString());
            }
            catch
            {
                html = html.Replace("{body}", "<br /><br />Could not connect to wind track database (pastebin.com)");
            }

            return html;
        }

        private static string DownloadPage(HttpListenerRequest request, string pageName)
        {
            string html = GetHtml(pageName);
            string downloadInfo = GetPostBody(request);

            if (!string.IsNullOrEmpty(downloadInfo))
            {
                string[] values = downloadInfo.Split('&');
                string[] filename = HttpUtility.UrlDecode(values[0]).Split('=');
                string[] url = HttpUtility.UrlDecode(values[1]).Split('=');

                HttpClient httpClient = new HttpClient();
                string windCodes = httpClient.GetStringAsync(url[1]).Result;

                html = html.Replace("{filename}", filename[1]);
                html = html.Replace("{url}", url[1]);
                html = html.Replace("{windtrack}", windCodes.Replace("\n", "<br \\>"));
            }

            return html;
        }

        private string ManagePage(HttpListenerRequest request, string pageName)
        {
            if(_waitForFile)
            {
                Thread.Sleep(250);
                _waitForFile = false;
            }
            string html = GetHtml(pageName);

            string[] files = Directory.GetFiles(_HTFanCtrl._videoTimecodePath);
            List<string> fileList = new List<string>(files);
            fileList.Sort();

            StringBuilder sb = new StringBuilder();
            foreach (string s in fileList)
            {
                sb.AppendFormat(@"<span style=""padding: 4px 8px;"" onclick=""editfile('{0}')"" class=""list-group-item list-group-item-action list-group-item-dark"">{1}</span>" + "\n", Path.GetFileName(s), Path.GetFileName(s).Replace(".txt", ""));
            }

            html = html.Replace("{body}", sb.ToString());

            return html;
        }

        private string EditPage(HttpListenerRequest request, string pageName)
        {
            string html = GetHtml(pageName);
            string editInfo = GetPostBody(request);

            if (!string.IsNullOrEmpty(editInfo))
            {
                string[] values = editInfo.Split('=');

                string windCodes = "";
                try
                {
                    windCodes = File.ReadAllText(Path.Combine(_HTFanCtrl._videoTimecodePath, HttpUtility.UrlDecode(values[1])));
                }
                catch { }

                html = html.Replace("{filename}", HttpUtility.UrlDecode(values[1]));
                html = html.Replace("{windtrack}", windCodes.Replace("\n", "<br \\>"));
            }

            return html;
        }

        private void UploadLocal(HttpListenerRequest request, string pageName)
        {
            string fileInfo = GetPostBody(request);
            try
            {
                int filenameStart = fileInfo.IndexOf("filename=") + 10;
                int filenameEnd = fileInfo.IndexOf('"', filenameStart);
                string filename = fileInfo[filenameStart..filenameEnd];

                int filedataStart = fileInfo.IndexOf("text/plain") + 14;
                int filedataEnd = fileInfo.IndexOf("------", filedataStart) - 4;
                string filedata = fileInfo[filedataStart..filedataEnd];

                File.WriteAllText(Path.Combine(_HTFanCtrl._videoTimecodePath, filename), filedata);
            }
            catch { }
        }

        private void RenameFile(HttpListenerRequest request, string pageName)
        {
            string renameInfo = GetPostBody(request);

            if (!string.IsNullOrEmpty(renameInfo) && !string.IsNullOrEmpty(_HTFanCtrl._currentVideoFileName))
            {
                try
                {
                    File.Move(Path.Combine(_HTFanCtrl._videoTimecodePath, renameInfo), Path.Combine(_HTFanCtrl._videoTimecodePath, _HTFanCtrl._currentVideoFileName + ".txt"), true);
                }
                catch { }
                _HTFanCtrl.ReInitialize(true);
            }
        }

        private void DeleteFile(HttpListenerRequest request, string pageName)
        {
            string deleteInfo = GetPostBody(request);

            if (!string.IsNullOrEmpty(deleteInfo))
            {
                try
                {
                    File.Delete(Path.Combine(_HTFanCtrl._videoTimecodePath, deleteInfo));
                }
                catch { }
                _HTFanCtrl.ReInitialize(true);
            }
        }

        private void SaveFile(HttpListenerRequest request, string pageName)
        {
            string saveInfo = GetPostBody(request);

            if (!string.IsNullOrEmpty(saveInfo))
            {
                string[] values = saveInfo.Split('=');
                HttpClient httpClient = new HttpClient();
                string windCodes = httpClient.GetStringAsync(values[0]).Result;

                string fileName = values[1];
                if (!string.IsNullOrEmpty(_HTFanCtrl._currentVideoFileName))
                {
                    fileName = _HTFanCtrl._currentVideoFileName;
                }

                try
                {
                    File.WriteAllText(Path.Combine(_HTFanCtrl._videoTimecodePath, fileName + ".txt"), windCodes);
                }
                catch { }

                _HTFanCtrl.ReInitialize(true);
            }
        }

        private string GetPlexPlayer(HttpListenerRequest request, string pageName)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                HttpClient httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

                using Stream s = httpClient.GetAsync($"http://{_HTFanCtrl._mediaPlayerIP}:{_HTFanCtrl._mediaPlayerPort}/clients?X-Plex-Token={_HTFanCtrl._PlexToken}").Result.Content.ReadAsStreamAsync().Result;
                XDocument plexml = XDocument.Load(s);
                IEnumerable<XElement> plexPlayers = plexml.Descendants("MediaContainer").Descendants("Server");

                foreach (XElement player in plexPlayers)
                {
                    sb.AppendFormat(@"<span style=""padding: 4px 8px;"" onclick=""Select('{0}', '{1}', {2}, '{3}')"" class=""list-group-item list-group-item-action list-group-item-dark"">{0} ({1})</span>" + "\n", player.Attribute("name").Value, player.Attribute("address").Value, player.Attribute("port").Value, player.Attribute("machineIdentifier").Value);
                }
            }
            catch { }

            return sb.ToString();
        }

        private void SavePlexPlayer(HttpListenerRequest request, string pageName)
        {
            string plexInfoJSON = GetPostBody(request);

            using JsonDocument data = JsonDocument.Parse(plexInfoJSON);

            _HTFanCtrl._plexClientName = data.RootElement.GetProperty("name").GetString();
            _HTFanCtrl._plexClientIP = data.RootElement.GetProperty("ip").GetString();
            _HTFanCtrl._plexClientPort = data.RootElement.GetProperty("port").GetRawText();
            _HTFanCtrl._plexClientGUID = data.RootElement.GetProperty("guid").GetString();

            _HTFanCtrl.SaveSettings();
            _HTFanCtrl.ReInitialize(true);
        }

        private string LoadedWindTrackData(HttpListenerRequest request, string pageName)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(GetCurrentMovie(request, pageName));
            sb.AppendLine("<br /><br />");
            sb.AppendLine("Current time: " + TimeSpan.FromMilliseconds(_HTFanCtrl._currentVideoTime).ToString("G").Substring(2, 12));
            sb.AppendLine("<br /><br />");

            if (_HTFanCtrl._videoTimeCodes != null)
            {
                for (int i = 0; i < _HTFanCtrl._videoTimeCodes.Count; i++)
                {
                    if(i == _HTFanCtrl._curCmdIndex)
                    {
                        sb.Append("<div style=\"color: lime; display: inline;\">");
                    }

                    sb.AppendLine(_HTFanCtrl._videoTimeCodes[i].Item1.ToString("G").Substring(2, 12).TrimEnd() + $",{_HTFanCtrl._videoTimeCodes[i].Item2}");

                    if (i == _HTFanCtrl._curCmdIndex)
                    {
                        sb.Append("</div>");
                    }

                    sb.AppendLine("<br />");
                }
            }
            else
            {
                sb.AppendLine("No wind track loaded");
            }

            return sb.ToString();
        }

        private string GetCurrentMovie(HttpListenerRequest request, string pageName)
        {
            string moviename = "No movie currently playing";
            if (!string.IsNullOrEmpty(_HTFanCtrl._currentVideoFileName))
            {
                moviename = $"Loaded Movie: {_HTFanCtrl._currentVideoFileName}";

                if(_HTFanCtrl._isPlaying)
                {
                    moviename = $"<b>{moviename}</b> <i>(playing)</i>";
                }
                else
                {
                    moviename = $"<b>{moviename}</b> <i>(stopped)</i>";
                }
            }

            return moviename;
        }

        private void FanCmd(HttpListenerRequest request, string pageName)
        {
            string fanCmdInfo = GetPostBody(request);

            byte[] cmd = Encoding.ASCII.GetBytes($"SEND_ONCE {_HTFanCtrl._lircRemote} {fanCmdInfo}\n");

            Console.WriteLine($"Sent CMD: {fanCmdInfo}");
            _HTFanCtrl.SendToLIRC(cmd);
        }

        private static string GetPostBody(HttpListenerRequest request)
        {
            string postData = null;
            if (request.HasEntityBody)
            {
                using Stream body = request.InputStream;
                using StreamReader reader = new StreamReader(body, request.ContentEncoding);
                postData = reader.ReadToEnd();
            }
            return postData;
        }

        private static string GetHtml(string fileName)
        {
            string html = "";
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                string resourceName = assembly.GetManifestResourceNames().Single(str => str.EndsWith(fileName + ".html"));

                using Stream stream = assembly.GetManifestResourceStream(resourceName);
                using StreamReader reader = new StreamReader(stream);
                html = reader.ReadToEnd();
            }
            catch { }

            return html;
        }
    }

    public static class ShellHelper
    {
        public static string Bash(this string cmd)
        {
            string escapedArgs = cmd.Replace("\"", "\\\"");

            Process process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{escapedArgs}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return result;
        }
    }
}