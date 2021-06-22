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
using OpenTK.Audio.OpenAL;
using System.IO.Compression;

namespace HTFanControl
{
    class WebUI
    {
        private readonly string _version = "Beta21";
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
                string urlpath;

                if (request.RawUrl.Contains('?'))
                {
                    urlpath = request.RawUrl.Remove(request.RawUrl.IndexOf('?'));
                }
                else
                {
                    urlpath = request.RawUrl;
                }

                switch (urlpath)
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
                    case "/uploadfile":
                        _waitForFile = true;
                        UploadFile(request, "uploadfile");
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
                        htmlResponse = GetPlexPlayers(request, "getplexplayers");
                        break;
                    case "/saveplexplayer":
                        _waitForFile = true;
                        SavePlexPlayer(request, "saveplexplayer");
                        break;
                    case "/selectaudiodevice":
                        htmlResponse = GetHtml("selectaudiodevice");
                        break;
                    case "/getaudiodevices":
                        htmlResponse = GetAudioDevices(request, "getaudiodevices");
                        break;
                    case "/saveaudiodevice":
                        _waitForFile = true;
                        SaveAudioDevice(request, "saveaudiodevice");
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
                    case "/selectvideo":
                        htmlResponse = SelectVideoPage(request, "selectvideo");
                        break;
                    case "/select":
                        _HTFanCtrl.SelectVideo(GetPostBody(request).Replace(".zip", ""));
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

            if(_HTFanCtrl._mediaPlayerType == "Audio")
            {
                html = html.Replace("{reloadbtn}", "Stop Audio Sync");
            }
            else
            {
                html = html.Replace("{reloadbtn}", "Reload Wind Track");
            }

            html = html.Replace("{version}", _version);

            return html;
        }

        private string GetStatusData(HttpListenerRequest request, string pageName)
        {
            string timeMsg = "Current time: ";
            if (_HTFanCtrl._mediaPlayerType == "Audio")
            {
                timeMsg = "Last time match: ";
            }

            StringBuilder htmlData = new StringBuilder();

            if (_HTFanCtrl != null)
            {
                htmlData.AppendLine(GetCurrentMovie(request, pageName));
                if(_HTFanCtrl._mediaPlayerType == "Audio")
                {
                    htmlData.AppendLine("<br />");
                    htmlData.AppendLine("<button onclick=\"window.location.href = 'selectvideo';\" class=\"btn btn-primary\">Select Video</button>");
                    htmlData.AppendLine("<br />");
                }
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
                htmlData.Append(timeMsg + TimeSpan.FromMilliseconds(_HTFanCtrl._currentVideoTime).ToString("G").Substring(2, 12));
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
                html = html.Replace("{Audio}", "");
                html = html.Replace("{lblPlayer}", "MPC-HC/BE IP");
            }
            else if (_HTFanCtrl._mediaPlayerType.Contains("Kodi"))
            {
                html = html.Replace("{MPC}", "");
                html = html.Replace("{Kodi}", "checked");
                html = html.Replace("{Plex}", "");
                html = html.Replace("{Audio}", "");
                html = html.Replace("{lblPlayer}", "Kodi IP");
            }
            else if (_HTFanCtrl._mediaPlayerType == "Plex")
            {
                html = html.Replace("{MPC}", "");
                html = html.Replace("{Kodi}", "");
                html = html.Replace("{Plex}", "checked");
                html = html.Replace("{Audio}", "");
                html = html.Replace("{lblPlayer}", "Plex Media Server IP");
            }
            else if (_HTFanCtrl._mediaPlayerType == "Audio")
            {
                html = html.Replace("{MPC}", "");
                html = html.Replace("{Kodi}", "");
                html = html.Replace("{Plex}", "");
                html = html.Replace("{Audio}", "checked");
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

            if (string.IsNullOrEmpty(_HTFanCtrl._audioDevice))
            {
                html = html.Replace("{AudioDevice}", "None");
            }
            else
            {
                html = html.Replace("{AudioDevice}", $"{_HTFanCtrl._audioDevice}");
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
            string searchQ = request.QueryString["searchQ"];

            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", ".netapp");
            try
            {
                string indexFile = httpClient.GetStringAsync("https://drive.google.com/uc?export=download&id=1spYA4n3g2QTh0hVLrRulLkL9J0wmRYKa").Result;
                List<string> videos = indexFile.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();

                List<string> matchResults = null;
                if (!string.IsNullOrEmpty(searchQ))
                {
                    matchResults = new List<string>();
                    string[] searchWords = searchQ.Split(' ');

                    foreach(string video in videos)
                    {
                        foreach(string s in searchWords)
                        {
                            if(video.ToLower().Contains(s.ToLower()))
                            {
                                matchResults.Add(video);
                                break;
                            }
                        }
                    }
                }

                if(matchResults != null)
                {
                    videos = matchResults;
                }

                StringBuilder sb = new StringBuilder();
                foreach (string s in videos)
                {
                    string[] values = s.Split("==");
                    sb.AppendFormat(@"<span style=""padding: 4px 8px;"" onclick=""downloadfile('{0}', '{1}')"" class=""list-group-item list-group-item-action list-group-item-dark"">{1}</span>" + "\n", values[1], values[0]);
                }

                html = html.Replace("{body}", sb.ToString());
            }
            catch
            {
                html = html.Replace("{body}", "<br /><br />Could not connect to wind track database.");
            }

            return html;
        }

        private string DownloadPage(HttpListenerRequest request, string pageName)
        {
            string html = GetHtml(pageName);
            string downloadInfo = GetPostBody(request);

            try
            {
                if (!string.IsNullOrEmpty(downloadInfo))
                {
                    string[] values = downloadInfo.Split('&');
                    string[] filename = HttpUtility.UrlDecode(values[0]).Split('=');
                    string url = HttpUtility.UrlDecode(values[1].Substring(4));

                    using WebClient client = new WebClient();
                    client.Headers.Add("User-Agent", ".netapp");
                    client.DownloadFile(url, Path.Combine(_HTFanCtrl._rootPath, "tmp", filename[1] + ".zip"));

                    string windCodes = "";
                    using ZipArchive zip = ZipFile.Open(Path.Combine(_HTFanCtrl._rootPath, "tmp", filename[1] + ".zip"), ZipArchiveMode.Read);
                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        if (entry.Name == "commands.txt")
                        {
                            using Stream s = entry.Open();
                            using StreamReader sr = new StreamReader(s);
                            windCodes = sr.ReadToEnd();
                        }
                    }

                    html = html.Replace("{filename}", filename[1]);
                    html = html.Replace("{windtrack}", windCodes.Replace("\n", "<br \\>"));
                }
            }
            catch { }

            return html;
        }

        private string ManagePage(HttpListenerRequest request, string pageName)
        {
            if(_waitForFile)
            {
                Thread.Sleep(500);
                _waitForFile = false;
            }
            string html = GetHtml(pageName);

            string[] files = Directory.GetFiles(Path.Combine(_HTFanCtrl._rootPath, "windtracks"));
            List<string> fileList = new List<string>(files);
            fileList.Sort();

            StringBuilder sb = new StringBuilder();
            foreach (string s in fileList)
            {
                sb.AppendFormat(@"<span style=""padding: 4px 8px;"" onclick=""editfile('{0}')"" class=""list-group-item list-group-item-action list-group-item-dark"">{1}</span>" + "\n", Path.GetFileName(s), Path.GetFileName(s).Replace(".txt", "").Replace(".zip", ""));
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
                    if (values[1].EndsWith(".txt"))
                    {
                        windCodes = File.ReadAllText(Path.Combine(_HTFanCtrl._rootPath, "windtracks", HttpUtility.UrlDecode(values[1])));
                    }
                    else
                    {
                        using ZipArchive zip = ZipFile.Open(Path.Combine(_HTFanCtrl._rootPath, "windtracks", HttpUtility.UrlDecode(values[1])), ZipArchiveMode.Read);
                        foreach (ZipArchiveEntry entry in zip.Entries)
                        {
                            if (entry.Name == "commands.txt")
                            {
                                using Stream s = entry.Open();
                                using StreamReader sr = new StreamReader(s);
                                windCodes = sr.ReadToEnd();
                            }
                        }
                    }
                }
                catch { }

                html = html.Replace("{filename}", HttpUtility.UrlDecode(values[1]));
                html = html.Replace("{windtrack}", windCodes.Replace("\n", "<br \\>"));
            }

            return html;
        }

        private void UploadFile(HttpListenerRequest request, string pageName)
        {
            string filename = HttpUtility.UrlDecode(Base64Decode(request.QueryString["filename"]));

            try
            {
                SaveUploadFile(request, Path.Combine(_HTFanCtrl._rootPath, "windtracks", filename));
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
                    string ext = Path.GetExtension(renameInfo);
                    File.Move(Path.Combine(_HTFanCtrl._rootPath, "windtracks", renameInfo), Path.Combine(_HTFanCtrl._rootPath, "windtracks", _HTFanCtrl._currentVideoFileName + ext), true);
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
                    File.Delete(Path.Combine(_HTFanCtrl._rootPath, "windtracks", deleteInfo));
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
                string fileName = saveInfo;
                if (!string.IsNullOrEmpty(_HTFanCtrl._currentVideoFileName))
                {
                    fileName = _HTFanCtrl._currentVideoFileName;
                }

                try
                {
                    File.Move(Path.Combine(_HTFanCtrl._rootPath, "tmp", saveInfo + ".zip"), Path.Combine(_HTFanCtrl._rootPath, "windtracks", fileName + ".zip"), true);
                }
                catch { }

                _HTFanCtrl.ReInitialize(true);
            }
        }

        private string GetPlexPlayers(HttpListenerRequest request, string pageName)
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

        private string GetAudioDevices(HttpListenerRequest request, string pageName)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                List<string> devices = ALC.GetString(AlcGetStringList.CaptureDeviceSpecifier);

                sb.AppendLine("If you do not see your audio input device listed here, make sure it is plugged in.");
                sb.AppendLine("<br/>");
                sb.AppendLine("You will need to restart HTFanControl to see any newly connected input devices.");
                sb.AppendLine("<br/><br/>");

                foreach (string device in devices)
                {
                    sb.AppendFormat(@"<span style=""padding: 4px 8px;"" onclick=""Select('{0}')"" class=""list-group-item list-group-item-action list-group-item-dark"">{0}</span>" + "\n", device);
                }
            }
            catch
            {
                if (ConfigHelper.GetOS() == "win")
                {
                    sb.AppendLine("OpenAL not installed.");
                    sb.AppendLine("<br/>");
                    sb.AppendLine(@"Please run OpenAL Windows Installer from <a href=""https://openal.org/downloads/"">Here</a>");
                }
            }

            return sb.ToString();
        }

        private void SaveAudioDevice(HttpListenerRequest request, string pageName)
        {
            string audioDevice = GetPostBody(request);

            _HTFanCtrl._audioDevice = audioDevice;

            _HTFanCtrl.SaveSettings();
            _HTFanCtrl.ReInitialize(true);
        }

        private string LoadedWindTrackData(HttpListenerRequest request, string pageName)
        {
            string timeMsg = "Current time: ";
            if (_HTFanCtrl._mediaPlayerType == "Audio")
            {
                timeMsg = "Last time match: ";
            }

            StringBuilder sb = new StringBuilder();

            sb.AppendLine(GetCurrentMovie(request, pageName));
            sb.AppendLine("<br /><br />");
            sb.AppendLine(timeMsg + TimeSpan.FromMilliseconds(_HTFanCtrl._currentVideoTime).ToString("G").Substring(2, 12));
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

        private string SelectVideoPage(HttpListenerRequest request, string pageName)
        {
            string html = GetHtml(pageName);

            string[] files = Directory.GetFiles(Path.Combine(_HTFanCtrl._rootPath, "windtracks"));
            List<string> fileList = new List<string>(files);
            fileList.Sort();

            StringBuilder sb = new StringBuilder();
            foreach (string s in fileList)
            {
                if (Path.GetExtension(s) == ".zip")
                {
                    sb.AppendFormat(@"<span style=""padding: 4px 8px;"" onclick=""selectfile('{0}')"" class=""list-group-item list-group-item-action list-group-item-dark"">{1}</span>" + "\n", Path.GetFileName(s), Path.GetFileName(s).Replace(".zip", ""));
                }
            }

            html = html.Replace("{body}", sb.ToString());

            return html;
        }

        private string GetCurrentMovie(HttpListenerRequest request, string pageName)
        {
            string moviename = "No video currently playing";
            if (_HTFanCtrl._mediaPlayerType == "Audio")
            {
                moviename = "No video currently selected";
            }
            if (!string.IsNullOrEmpty(_HTFanCtrl._currentVideoFileName))
            {
                moviename = $"Loaded Video: {_HTFanCtrl._currentVideoFileName}";

                if (_HTFanCtrl._mediaPlayerType != "Audio")
                {
                    if (_HTFanCtrl._isPlaying)
                    {
                        moviename = $"<b>{moviename}</b> <i>(playing)</i>";
                    }
                    else
                    {
                        moviename = $"<b>{moviename}</b> <i>(stopped)</i>";
                    }
                }
                else
                {
                    moviename = $"<b>{moviename}</b> <i>{_HTFanCtrl._audioSync.State}</i>";
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

        private static string Base64Decode(string base64EncodedData)
        {
            if (!string.IsNullOrEmpty(base64EncodedData))
            {
                byte[] base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
                return Encoding.UTF8.GetString(base64EncodedBytes);
            }
            return null;
        }

        private static void SaveUploadFile(HttpListenerRequest request, string filePath)
        {
            string boundary = "--" + request.ContentType.Split(';')[1].Split('=')[1];
            Byte[] boundaryBytes = request.ContentEncoding.GetBytes(boundary);
            Int32 boundaryLen = boundaryBytes.Length;

            using (FileStream output = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                Byte[] buffer = new Byte[1024];
                Int32 len = request.InputStream.Read(buffer, 0, 1024);
                Int32 startPos = -1;

                // Find start boundary
                while (true)
                {
                    if (len == 0)
                    {
                        throw new Exception("Start Boundaray Not Found");
                    }

                    startPos = IndexOf(buffer, len, boundaryBytes);
                    if (startPos >= 0)
                    {
                        break;
                    }
                    else
                    {
                        Array.Copy(buffer, len - boundaryLen, buffer, 0, boundaryLen);
                        len = request.InputStream.Read(buffer, boundaryLen, 1024 - boundaryLen);
                    }
                }

                // Skip four lines (Boundary, Content-Disposition, Content-Type, and a blank)
                for (Int32 i = 0; i < 4; i++)
                {
                    while (true)
                    {
                        if (len == 0)
                        {
                            throw new Exception("Preamble not Found.");
                        }

                        startPos = Array.IndexOf(buffer, request.ContentEncoding.GetBytes("\n")[0], startPos);
                        if (startPos >= 0)
                        {
                            startPos++;
                            break;
                        }
                        else
                        {
                            len = request.InputStream.Read(buffer, 0, 1024);
                        }
                    }
                }

                Array.Copy(buffer, startPos, buffer, 0, len - startPos);
                len = len - startPos;

                while (true)
                {
                    Int32 endPos = IndexOf(buffer, len, boundaryBytes);
                    if (endPos >= 0)
                    {
                        if (endPos > 0) output.Write(buffer, 0, endPos - 2);
                        break;
                    }
                    else if (len <= boundaryLen)
                    {
                        throw new Exception("End Boundaray Not Found");
                    }
                    else
                    {
                        output.Write(buffer, 0, len - boundaryLen);
                        Array.Copy(buffer, len - boundaryLen, buffer, 0, boundaryLen);
                        len = request.InputStream.Read(buffer, boundaryLen, 1024 - boundaryLen) + boundaryLen;
                    }
                }
            }
        }

        private static Int32 IndexOf(Byte[] buffer, Int32 len, Byte[] boundaryBytes)
        {
            for (Int32 i = 0; i <= len - boundaryBytes.Length; i++)
            {
                Boolean match = true;
                for (Int32 j = 0; j < boundaryBytes.Length && match; j++)
                {
                    match = buffer[i + j] == boundaryBytes[j];
                }

                if (match)
                {
                    return i;
                }
            }

            return -1;
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