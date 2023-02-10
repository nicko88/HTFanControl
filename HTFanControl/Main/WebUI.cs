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
using HTFanControl.Util;

namespace HTFanControl.Main
{
    class WebUI
    {
        private readonly string _version = Assembly.GetExecutingAssembly().GetName().Version.ToString().TrimEnd(new char[] { '.', '0' });
        private readonly Thread _httpThread;
        private readonly HTFanControl _HTFanCtrl;
        private string _port;
        private string _instanceName;
        private bool _webUIEnabled = true;
        private bool _waitForFile = false;

        public WebUI(string port, string instanceName)
        {
            _port = port;
            _instanceName = instanceName;
            _httpThread = new Thread(StartListen);
            _httpThread.Start();

            _HTFanCtrl = new HTFanControl();
        }

        private void StartListen()
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add($"http://*:{_port}/");
            listener.Start();

            while (_webUIEnabled)
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
                        htmlResponse = StatusPage();
                        break;
                    case "/statusdata":
                        htmlResponse = GetStatusData();
                        break;
                    case "/currentmovie":
                        htmlResponse = GetCurrentMovie();
                        break;
                    case "/settings":
                        htmlResponse = SettingsPage();
                        break;
                    case "/savesettings":
                        SaveSettings(request);
                        break;
                    case "/downloadlist":
                        htmlResponse = DownloadListPage(request);
                        break;
                    case "/download":
                        htmlResponse = DownloadPage(request);
                        break;
                    case "/manage":
                        htmlResponse = ManagePage();
                        break;
                    case "/edit":
                        htmlResponse = EditPage(request);
                        break;
                    case "/add":
                        htmlResponse = GetHtml("add");
                        break;
                    case "/uploadfile":
                        UploadFile(request);
                        break;
                    case "/rename":
                        RenameFile(request);
                        break;
                    case "/delete":
                        DeleteFile(request);
                        break;
                    case "/save":
                        SaveFile(request);
                        break;
                    case "/selectplexplayer":
                        htmlResponse = GetHtml("selectplexplayer");
                        break;
                    case "/getplexplayers":
                        htmlResponse = GetPlexPlayers();
                        break;
                    case "/saveplexplayer":
                        SavePlexPlayer(request);
                        break;
                    case "/selectaudiodevice":
                        htmlResponse = GetHtml("selectaudiodevice");
                        break;
                    case "/getaudiodevices":
                        htmlResponse = GetAudioDevices();
                        break;
                    case "/saveaudiodevice":
                        SaveAudioDevice(request);
                        break;
                    case "/reload":
                        _HTFanCtrl.ReInitialize(true);
                        break;
                    case "/togglefan":
                        _HTFanCtrl.ToggleFan();
                        break;
                    case "/toggleoffset":
                        _HTFanCtrl._offsetEnabled = !_HTFanCtrl._offsetEnabled;
                        _HTFanCtrl.ReInitialize(false);
                        break;
                    case "/fantester":
                        _HTFanCtrl.ReInitialize(false);
                        htmlResponse = GetHtml("fantester");
                        break;
                    case "/loadedwindtrack":
                        htmlResponse = GetHtml("loadedwindtrack");
                        break;
                    case "/loadedwindtrackdata":
                        htmlResponse = LoadedWindTrackData();
                        break;
                    case "/logviewer":
                        htmlResponse = GetHtml("logviewer");
                        break;
                    case "/logdata":
                        htmlResponse = LogData();
                        break;
                    case "/crashlogs":
                        htmlResponse = CrashlogsPage();
                        break;
                    case "/viewcrashlog":
                        htmlResponse = ViewCrashlog(request);
                        break;
                    case "/deletelogs":
                        try
                        {
                            Directory.Delete(Path.Combine(ConfigHelper._rootPath, "crashlogs"), true);
                        }
                        catch { }
                        break;
                    case "/selectvideo":
                        htmlResponse = SelectVideoPage();
                        break;
                    case "/select":
                        _HTFanCtrl.SelectVideo(GetPostBody(request).Replace(".zip", ""));
                        break;
                    case "/fancmd":
                        FanCmd(request);
                        break;
                    case "/clearerror":
                        _HTFanCtrl._errorStatus = null;
                        break;
                    case "/checkupdate":
                        htmlResponse = CheckUpdatePage();
                        break;
                    case "/linuxupdate":
                        LinuxUpdate();
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

        private string StatusPage()
        {
            string html = GetHtml("status");
            html = html.Replace("HTFanControl", _instanceName);

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

            if(_HTFanCtrl._settings.MediaPlayerType == "Audio")
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

        private string GetStatusData()
        {
            string timeMsg = "Current time: ";
            if (_HTFanCtrl._settings.MediaPlayerType == "Audio")
            {
                timeMsg = "Last time match: ";
            }

            StringBuilder htmlData = new StringBuilder();

            if (_HTFanCtrl != null)
            {
                htmlData.AppendLine(GetCurrentMovie());
                if(_HTFanCtrl._settings.MediaPlayerType == "Audio")
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

                    if (_HTFanCtrl._hasOffset && _HTFanCtrl._settings.MediaPlayerType != "Audio")
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
                htmlData.Append(timeMsg + TimeSpan.FromMilliseconds(_HTFanCtrl._loadedVideoTime).ToString("G").Substring(2, 12));
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
                    if (!string.IsNullOrEmpty(_HTFanCtrl._loadedVideoFilename) && _HTFanCtrl._loadedVideoFilename != "Loading Video Fingerprints...")
                    {
                        htmlData.AppendLine("No wind track file found named: " + _HTFanCtrl._loadedVideoFilename + ".zip/.txt");
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

        private string SettingsPage()
        {
            if (_waitForFile)
            {
                Thread.Sleep(500);
                _waitForFile = false;
            }

            string html = GetHtml("settings");

            //if (ConfigHelper.GetOS() == "win")
            //{
                html = html.Replace("/*{showlinux}*/", "display: none;");
                html = html.Replace("/*{hidelinux}*/", "display: initial;");
            //}
            //else
            //{
            //    html = html.Replace("/*{showlinux}*/", "display: initial;");
            //    html = html.Replace("/*{hidelinux}*/", "display: none;");
            //}

            if (_HTFanCtrl._settings.ControllerType == "LIRC")
            {
                html = html.Replace("{LIRC}", "checked");
                html = html.Replace("{MQTT}", "");
                html = html.Replace("{HTTP}", "");
            }
            else if (_HTFanCtrl._settings.ControllerType == "MQTT")
            {
                html = html.Replace("{LIRC}", "");
                html = html.Replace("{MQTT}", "checked");
                html = html.Replace("{HTTP}", "");
            }
            else if (_HTFanCtrl._settings.ControllerType == "HTTP")
            {
                html = html.Replace("{LIRC}", "");
                html = html.Replace("{MQTT}", "");
                html = html.Replace("{HTTP}", "checked");
            }

            html = html.Replace("{LircIP}", _HTFanCtrl._settings.LIRC_IP);
            html = html.Replace("{LircPort}", _HTFanCtrl._settings.LIRC_Port.ToString());
            html = html.Replace("{LircRemote}", _HTFanCtrl._settings.LIRC_Remote);
            html = html.Replace("{LircONdelay}", _HTFanCtrl._settings.LIRC_ON_Delay.ToString());

            html = html.Replace("{MqttIP}", _HTFanCtrl._settings.MQTT_IP);
            html = html.Replace("{MqttPort}", _HTFanCtrl._settings.MQTT_Port.ToString());

            html = html.Replace("{MqttUser}", _HTFanCtrl._settings.MQTT_User);
            html = html.Replace("{MqttPass}", _HTFanCtrl._settings.MQTT_Pass);

            html = html.Replace("{MqttOFFtopic}", HttpUtility.HtmlEncode(_HTFanCtrl._settings.MQTT_OFF_Topic));
            html = html.Replace("{MqttOFFpayload}", HttpUtility.HtmlEncode(_HTFanCtrl._settings.MQTT_OFF_Payload));
            html = html.Replace("{MqttECOtopic}", HttpUtility.HtmlEncode(_HTFanCtrl._settings.MQTT_ECO_Topic));
            html = html.Replace("{MqttECOpayload}", HttpUtility.HtmlEncode(_HTFanCtrl._settings.MQTT_ECO_Payload));
            html = html.Replace("{MqttLOWtopic}", HttpUtility.HtmlEncode(_HTFanCtrl._settings.MQTT_LOW_Topic));
            html = html.Replace("{MqttLOWpayload}", HttpUtility.HtmlEncode(_HTFanCtrl._settings.MQTT_LOW_Payload));
            html = html.Replace("{MqttMEDtopic}", HttpUtility.HtmlEncode(_HTFanCtrl._settings.MQTT_MED_Topic));
            html = html.Replace("{MqttMEDpayload}", HttpUtility.HtmlEncode(_HTFanCtrl._settings.MQTT_MED_Payload));
            html = html.Replace("{MqttHIGHtopic}", HttpUtility.HtmlEncode(_HTFanCtrl._settings.MQTT_HIGH_Topic));
            html = html.Replace("{MqttHIGHpayload}", HttpUtility.HtmlEncode(_HTFanCtrl._settings.MQTT_HIGH_Payload));
            html = html.Replace("{MqttONtopic}", HttpUtility.HtmlEncode(_HTFanCtrl._settings.MQTT_ON_Topic));
            html = html.Replace("{MqttONpayload}", HttpUtility.HtmlEncode(_HTFanCtrl._settings.MQTT_ON_Payload));
            html = html.Replace("{MqttONdelay}", HttpUtility.HtmlEncode(_HTFanCtrl._settings.MQTT_ON_Delay));

            html = html.Replace("{HttpUser}", _HTFanCtrl._settings.HTTP_User);
            html = html.Replace("{HttpPass}", _HTFanCtrl._settings.HTTP_Pass);

            html = html.Replace("{HttpOFFurl}", HttpUtility.HtmlEncode(_HTFanCtrl._settings.HTTP_OFF_URL));
            html = html.Replace("{HttpOFFpayload}", HttpUtility.HtmlEncode(_HTFanCtrl._settings.HTTP_OFF_Payload));
            html = html.Replace("{HttpECOurl}", HttpUtility.HtmlEncode(_HTFanCtrl._settings.HTTP_ECO_URL));
            html = html.Replace("{HttpECOpayload}", HttpUtility.HtmlEncode(_HTFanCtrl._settings.HTTP_ECO_Payload));
            html = html.Replace("{HttpLOWurl}", HttpUtility.HtmlEncode(_HTFanCtrl._settings.HTTP_LOW_URL));
            html = html.Replace("{HttpLOWpayload}", HttpUtility.HtmlEncode(_HTFanCtrl._settings.HTTP_LOW_Payload));
            html = html.Replace("{HttpMEDurl}", HttpUtility.HtmlEncode(_HTFanCtrl._settings.HTTP_MED_URL));
            html = html.Replace("{HttpMEDpayload}", HttpUtility.HtmlEncode(_HTFanCtrl._settings.HTTP_MED_Payload));
            html = html.Replace("{HttpHIGHurl}", HttpUtility.HtmlEncode(_HTFanCtrl._settings.HTTP_HIGH_URL));
            html = html.Replace("{HttpHIGHpayload}", HttpUtility.HtmlEncode(_HTFanCtrl._settings.HTTP_HIGH_Payload));

            if (_HTFanCtrl._settings.MediaPlayerType == "MPC")
            {
                html = html.Replace("{MPC}", "checked");
                html = html.Replace("{Kodi}", "");
                html = html.Replace("{Plex}", "");
                html = html.Replace("{RokuPlex}", "");
                html = html.Replace("{Audio}", "");
                html = html.Replace("{lblPlayer}", "MPC-HC/BE IP");
            }
            else if (_HTFanCtrl._settings.MediaPlayerType.Contains("Kodi"))
            {
                html = html.Replace("{MPC}", "");
                html = html.Replace("{Kodi}", "checked");
                html = html.Replace("{Plex}", "");
                html = html.Replace("{RokuPlex}", "");
                html = html.Replace("{Audio}", "");
                html = html.Replace("{lblPlayer}", "Kodi IP");
            }
            else if (_HTFanCtrl._settings.MediaPlayerType == "Plex")
            {
                html = html.Replace("{MPC}", "");
                html = html.Replace("{Kodi}", "");
                html = html.Replace("{Plex}", "checked");
                html = html.Replace("{RokuPlex}", "");
                html = html.Replace("{Audio}", "");
                html = html.Replace("{lblPlayer}", "Plex Media Server IP");
            }
            else if (_HTFanCtrl._settings.MediaPlayerType == "RokuPlex")
            {
                html = html.Replace("{MPC}", "");
                html = html.Replace("{Kodi}", "");
                html = html.Replace("{Plex}", "");
                html = html.Replace("{RokuPlex}", "checked");
                html = html.Replace("{Audio}", "");
                html = html.Replace("{lblPlayer}", "Plex Media Server IP");
            }
            else if (_HTFanCtrl._settings.MediaPlayerType == "Audio")
            {
                html = html.Replace("{MPC}", "");
                html = html.Replace("{Kodi}", "");
                html = html.Replace("{Plex}", "");
                html = html.Replace("{Audio}", "checked");
            }

            html = html.Replace("{MediaPlayerIP}", _HTFanCtrl._settings.MediaPlayerIP);
            html = html.Replace("{MediaPlayerPort}", _HTFanCtrl._settings.MediaPlayerPort.ToString());

            html = html.Replace("{PlexToken}", _HTFanCtrl._settings.PlexToken);

            if (string.IsNullOrEmpty(_HTFanCtrl._settings.PlexClientName))
            {
                html = html.Replace("{PlexPlayer}", "None");
            }
            else
            {
                html = html.Replace("{PlexPlayer}", $"{_HTFanCtrl._settings.PlexClientName} ({_HTFanCtrl._settings.PlexClientIP})");
            }

            if (string.IsNullOrEmpty(_HTFanCtrl._settings.AudioDevice))
            {
                html = html.Replace("{AudioDevice}", "None");
            }
            else
            {
                html = html.Replace("{AudioDevice}", $"{_HTFanCtrl._settings.AudioDevice}");
            }

            html = html.Replace("{GlobalOffset}", _HTFanCtrl._settings.GlobalOffsetMS.ToString());
            html = html.Replace("{ECOSpinupOffset}", _HTFanCtrl._settings.ECOSpinupOffsetMS.ToString());
            html = html.Replace("{LOWSpinupOffset}", _HTFanCtrl._settings.LOWSpinupOffsetMS.ToString());
            html = html.Replace("{MEDSpinupOffset}", _HTFanCtrl._settings.MEDSpinupOffsetMS.ToString());
            html = html.Replace("{HIGHSpinupOffset}", _HTFanCtrl._settings.HIGHSpinupOffsetMS.ToString());
            html = html.Replace("{SpindownOffset}", _HTFanCtrl._settings.SpindownOffsetMS.ToString());

            html = html.Replace("{version}", @$"Version: {_version} <a href=""checkupdate"">(Check For Update)</a>");

            return html;
        }

        private void SaveSettings(HttpListenerRequest request)
        {
            string settingsInfoJSON = GetPostBody(request);

            if (!string.IsNullOrEmpty(settingsInfoJSON))
            {
                using JsonDocument data = JsonDocument.Parse(settingsInfoJSON);

                _HTFanCtrl._settings.ControllerType = data.RootElement.GetProperty("Controller").GetString();
                _HTFanCtrl._settings.LIRC_IP = data.RootElement.GetProperty("LircIP").GetString();
                _HTFanCtrl._settings.LIRC_Port = int.TryParse(data.RootElement.GetProperty("LircPort").GetString(), out int LircPort) ? LircPort : 8765;
                _HTFanCtrl._settings.LIRC_Remote = data.RootElement.GetProperty("LircRemote").GetString();
                _HTFanCtrl._settings.LIRC_ON_Delay = int.TryParse(data.RootElement.GetProperty("LircONdelay").GetString(), out int LircONdelay) ? LircONdelay : 0;
                _HTFanCtrl._settings.MQTT_IP = data.RootElement.GetProperty("MqttIP").GetString();
                _HTFanCtrl._settings.MQTT_Port = int.TryParse(data.RootElement.GetProperty("MqttPort").GetString(), out int MqttPort) ? MqttPort : 1883;
                _HTFanCtrl._settings.MQTT_User = data.RootElement.GetProperty("MqttUser").GetString();
                _HTFanCtrl._settings.MQTT_Pass = data.RootElement.GetProperty("MqttPass").GetString();
                _HTFanCtrl._settings.MQTT_OFF_Topic = data.RootElement.GetProperty("MqttOFFtopic").GetString();
                _HTFanCtrl._settings.MQTT_OFF_Payload = data.RootElement.GetProperty("MqttOFFpayload").GetString();
                _HTFanCtrl._settings.MQTT_ECO_Topic = data.RootElement.GetProperty("MqttECOtopic").GetString();
                _HTFanCtrl._settings.MQTT_ECO_Payload = data.RootElement.GetProperty("MqttECOpayload").GetString();
                _HTFanCtrl._settings.MQTT_LOW_Topic = data.RootElement.GetProperty("MqttLOWtopic").GetString();
                _HTFanCtrl._settings.MQTT_LOW_Payload = data.RootElement.GetProperty("MqttLOWpayload").GetString();
                _HTFanCtrl._settings.MQTT_MED_Topic = data.RootElement.GetProperty("MqttMEDtopic").GetString();
                _HTFanCtrl._settings.MQTT_MED_Payload = data.RootElement.GetProperty("MqttMEDpayload").GetString();
                _HTFanCtrl._settings.MQTT_HIGH_Topic = data.RootElement.GetProperty("MqttHIGHtopic").GetString();
                _HTFanCtrl._settings.MQTT_HIGH_Payload = data.RootElement.GetProperty("MqttHIGHpayload").GetString();
                _HTFanCtrl._settings.MQTT_ON_Topic = data.RootElement.GetProperty("MqttONtopic").GetString();
                _HTFanCtrl._settings.MQTT_ON_Payload = data.RootElement.GetProperty("MqttONpayload").GetString();
                _HTFanCtrl._settings.MQTT_ON_Delay = int.TryParse(data.RootElement.GetProperty("MqttONdelay").GetString(), out int MqttONdelay) ? MqttONdelay: 0;
                _HTFanCtrl._settings.HTTP_User = data.RootElement.GetProperty("HttpUser").GetString();
                _HTFanCtrl._settings.HTTP_Pass = data.RootElement.GetProperty("HttpPass").GetString();
                _HTFanCtrl._settings.HTTP_OFF_URL = data.RootElement.GetProperty("HttpOFFurl").GetString();
                _HTFanCtrl._settings.HTTP_OFF_Payload = data.RootElement.GetProperty("HttpOFFpayload").GetString();
                _HTFanCtrl._settings.HTTP_ECO_URL = data.RootElement.GetProperty("HttpECOurl").GetString();
                _HTFanCtrl._settings.HTTP_ECO_Payload = data.RootElement.GetProperty("HttpECOpayload").GetString();
                _HTFanCtrl._settings.HTTP_LOW_URL = data.RootElement.GetProperty("HttpLOWurl").GetString();
                _HTFanCtrl._settings.HTTP_LOW_Payload = data.RootElement.GetProperty("HttpLOWpayload").GetString();
                _HTFanCtrl._settings.HTTP_MED_URL = data.RootElement.GetProperty("HttpMEDurl").GetString();
                _HTFanCtrl._settings.HTTP_MED_Payload = data.RootElement.GetProperty("HttpMEDpayload").GetString();
                _HTFanCtrl._settings.HTTP_HIGH_URL = data.RootElement.GetProperty("HttpHIGHurl").GetString();
                _HTFanCtrl._settings.HTTP_HIGH_Payload = data.RootElement.GetProperty("HttpHIGHpayload").GetString();
                _HTFanCtrl._settings.MediaPlayerIP = data.RootElement.GetProperty("MediaPlayerIP").GetString();
                _HTFanCtrl._settings.MediaPlayerPort = int.TryParse(data.RootElement.GetProperty("MediaPlayerPort").GetString(), out int MediaPlayerPort) ? MediaPlayerPort : 0;
                _HTFanCtrl._settings.GlobalOffsetMS = int.TryParse(data.RootElement.GetProperty("GlobalOffset").GetString(), out int GlobalOffset) ? GlobalOffset : 0;
                _HTFanCtrl._settings.ECOSpinupOffsetMS = int.TryParse(data.RootElement.GetProperty("ECOSpinupOffset").GetString(), out int ECOSpinupOffset) ? ECOSpinupOffset : 0;
                _HTFanCtrl._settings.LOWSpinupOffsetMS = int.TryParse(data.RootElement.GetProperty("LOWSpinupOffset").GetString(), out int LOWSpinupOffset) ? LOWSpinupOffset : 0;
                _HTFanCtrl._settings.MEDSpinupOffsetMS = int.TryParse(data.RootElement.GetProperty("MEDSpinupOffset").GetString(), out int MEDSpinupOffset) ? MEDSpinupOffset : 0;
                _HTFanCtrl._settings.HIGHSpinupOffsetMS = int.TryParse(data.RootElement.GetProperty("HIGHSpinupOffset").GetString(), out int HIGHSpinupOffset) ? HIGHSpinupOffset : 0;
                _HTFanCtrl._settings.SpindownOffsetMS = int.TryParse(data.RootElement.GetProperty("SpindownOffset").GetString(), out int SpindownOffset) ? SpindownOffset : 0;
                _HTFanCtrl._settings.MediaPlayerType = data.RootElement.GetProperty("MediaPlayer").GetString();
                _HTFanCtrl._settings.PlexToken = data.RootElement.GetProperty("PlexToken").GetString();

                Settings.SaveSettings(_HTFanCtrl._settings);
                _HTFanCtrl.ReInitialize(true);
            }
        }

        private string CheckUpdatePage()
        {
            string html = GetHtml("checkupdate");

            StringBuilder sb = new StringBuilder();
            try
            {
                sb.AppendLine("</br>");
                sb.AppendLine($"<b>Current Version:</b> {_version}");
                sb.AppendLine("</br>");

                using HttpClient httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", ".netapp");
                string releaseJSON = httpClient.GetStringAsync("https://api.github.com/repos/nicko88/htfancontrol/releases/latest").Result;
                using JsonDocument data = JsonDocument.Parse(releaseJSON);
                string latest = data.RootElement.GetProperty("tag_name").GetString();
                string latesturl = data.RootElement.GetProperty("html_url").GetString();

                sb.AppendLine($"<b>Latest Version:</b> {latest}");
                sb.AppendLine("</br></br>");

                if (_version != latest)
                {
                    if (ConfigHelper.GetOS() != "win")
                    {
                        sb.AppendLine(@"<button id=""btnupdate"" onclick=""linuxupdate();"" class=""btn btn-primary"">Update HTFanControl</button>");
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

        private void LinuxUpdate()
        {
            $"wget -O {Path.Combine(ConfigHelper._rootPath, "update.sh")} https://raw.githubusercontent.com/nicko88/HTFanControl/master/install/update.sh".Bash();
            $"bash {Path.Combine(ConfigHelper._rootPath, "update.sh")}".Bash();

            _webUIEnabled = false;
        }

        private static string DownloadListPage(HttpListenerRequest request)
        {
            string html = GetHtml("downloadlist");
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

        private static string DownloadPage(HttpListenerRequest request)
        {
            string html = GetHtml("download");
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
                    client.DownloadFile(url, Path.Combine(ConfigHelper._rootPath, "tmp", filename[1] + ".zip"));

                    string windCodes = "";
                    using ZipArchive zip = ZipFile.Open(Path.Combine(ConfigHelper._rootPath, "tmp", filename[1] + ".zip"), ZipArchiveMode.Read);
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

        private string ManagePage()
        {
            if(_waitForFile)
            {
                Thread.Sleep(750);
                _waitForFile = false;
            }
            string html = GetHtml("manage");

            string[] files = Directory.GetFiles(Path.Combine(ConfigHelper._rootPath, "windtracks"));
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

        private static string EditPage(HttpListenerRequest request)
        {
            string html = GetHtml("edit");
            string editInfo = GetPostBody(request);

            if (!string.IsNullOrEmpty(editInfo))
            {
                string[] values = editInfo.Split('=');

                string windCodes = "";
                try
                {
                    if (values[1].EndsWith(".txt"))
                    {
                        windCodes = File.ReadAllText(Path.Combine(ConfigHelper._rootPath, "windtracks", HttpUtility.UrlDecode(values[1])));
                    }
                    else
                    {
                        using ZipArchive zip = ZipFile.Open(Path.Combine(ConfigHelper._rootPath, "windtracks", HttpUtility.UrlDecode(values[1])), ZipArchiveMode.Read);
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

        private void UploadFile(HttpListenerRequest request)
        {
            _waitForFile = true;

            string filename = HttpUtility.UrlDecode(Base64Decode(request.QueryString["filename"]));

            try
            {
                SaveUploadFile(request, Path.Combine(ConfigHelper._rootPath, "windtracks", filename));
            }
            catch { }
        }

        private void RenameFile(HttpListenerRequest request)
        {
            string renameInfo = GetPostBody(request);

            if (!string.IsNullOrEmpty(renameInfo) && !string.IsNullOrEmpty(_HTFanCtrl._loadedVideoFilename))
            {
                try
                {
                    string ext = Path.GetExtension(renameInfo);
                    File.Move(Path.Combine(ConfigHelper._rootPath, "windtracks", renameInfo), Path.Combine(ConfigHelper._rootPath, "windtracks", _HTFanCtrl._loadedVideoFilename + ext), true);
                }
                catch { }
                _HTFanCtrl.ReInitialize(false);
            }
        }

        private void DeleteFile(HttpListenerRequest request)
        {
            _waitForFile = true;

            string deleteInfo = GetPostBody(request);

            if (!string.IsNullOrEmpty(deleteInfo))
            {
                try
                {
                    File.Delete(Path.Combine(ConfigHelper._rootPath, "windtracks", deleteInfo));
                }
                catch { }
                _HTFanCtrl.ReInitialize(false);
            }
        }

        private void SaveFile(HttpListenerRequest request)
        {
            string saveInfo = GetPostBody(request);

            if (!string.IsNullOrEmpty(saveInfo))
            {
                string fileName = saveInfo;
                if (!string.IsNullOrEmpty(_HTFanCtrl._loadedVideoFilename))
                {
                    fileName = _HTFanCtrl._loadedVideoFilename;
                }

                try
                {
                    File.Move(Path.Combine(ConfigHelper._rootPath, "tmp", saveInfo + ".zip"), Path.Combine(ConfigHelper._rootPath, "windtracks", fileName + ".zip"), true);
                }
                catch { }

                _HTFanCtrl.ReInitialize(false);
            }
        }

        private string GetPlexPlayers()
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                HttpClient httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

                using Stream s = httpClient.GetAsync($"http://{_HTFanCtrl._settings.MediaPlayerIP}:{_HTFanCtrl._settings.MediaPlayerPort}/clients?X-Plex-Token={_HTFanCtrl._settings.PlexToken}").Result.Content.ReadAsStreamAsync().Result;
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

        private void SavePlexPlayer(HttpListenerRequest request)
        {
            _waitForFile = true;

            string plexInfoJSON = GetPostBody(request);

            using JsonDocument data = JsonDocument.Parse(plexInfoJSON);

            _HTFanCtrl._settings.PlexClientName = data.RootElement.GetProperty("name").GetString();
            _HTFanCtrl._settings.PlexClientIP = data.RootElement.GetProperty("ip").GetString();
            _HTFanCtrl._settings.PlexClientPort = data.RootElement.GetProperty("port").GetRawText();
            _HTFanCtrl._settings.PlexClientGUID = data.RootElement.GetProperty("guid").GetString();

            Settings.SaveSettings(_HTFanCtrl._settings);
            _HTFanCtrl.ReInitialize(false);
        }

        private static string GetAudioDevices()
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
                    if (device != "ALSA Default")
                    {
                        sb.AppendFormat(@"<span style=""padding: 4px 8px;"" onclick=""Select('{0}')"" class=""list-group-item list-group-item-action list-group-item-dark"">{0}</span>" + "\n", device);
                    }
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

        private void SaveAudioDevice(HttpListenerRequest request)
        {
            _waitForFile = true;

            string audioDevice = GetPostBody(request);

            _HTFanCtrl._settings.AudioDevice = audioDevice;

            Settings.SaveSettings(_HTFanCtrl._settings);
            _HTFanCtrl.ReInitialize(false);
        }

        private string LoadedWindTrackData()
        {
            string timeMsg = "Current time: ";
            if (_HTFanCtrl._settings.MediaPlayerType == "Audio")
            {
                timeMsg = "Last time match: ";
            }

            StringBuilder sb = new StringBuilder();

            sb.AppendLine(GetCurrentMovie());
            sb.AppendLine("<br /><br />");
            sb.AppendLine(timeMsg + TimeSpan.FromMilliseconds(_HTFanCtrl._loadedVideoTime).ToString("G").Substring(2, 12));
            sb.AppendLine("<br /><br />");

            if (_HTFanCtrl._videoTimeCodes != null)
            {
                sb.Append(@"<div class=""text-monospace"">");

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

                sb.Append("</div>");
            }
            else
            {
                sb.AppendLine("No wind track loaded");
            }

            return sb.ToString();
        }

        private string LogData()
        {
            string timeMsg = "Current time: ";
            if (_HTFanCtrl._settings.MediaPlayerType == "Audio")
            {
                timeMsg = "Last time match: ";
            }

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("<br />");
            sb.AppendLine(@"<a href=""crashlogs"">(View Crashlogs)</a>");
            sb.AppendLine("<br /><br />");

            sb.AppendLine(GetCurrentMovie());
            sb.AppendLine("<br /><br />");
            sb.AppendLine(timeMsg + TimeSpan.FromMilliseconds(_HTFanCtrl._loadedVideoTime).ToString("G").Substring(2, 12));
            sb.AppendLine("<br /><br />");

            sb.Append(@"<div class=""text-monospace"" style=""font-size: 0.75rem;"">");

            foreach (string s in _HTFanCtrl._log.RealtimeLog)
            {
                sb.AppendLine(s);
                sb.AppendLine("<br />");
            }

            sb.Append("</div>");

            return sb.ToString();
        }

        private static string CrashlogsPage()
        {
            string html = GetHtml("crashlogs");
            string[] crashlogs = new string[0];
            try
            {
                crashlogs = Directory.GetFiles(Path.Combine(ConfigHelper._rootPath, "crashlogs"));
            }
            catch { }

            StringBuilder sb = new StringBuilder();
            foreach (string s in crashlogs)
            {
                sb.AppendFormat(@"<span style=""padding: 4px 8px;"" onclick=""viewcrashlog('{0}')"" class=""list-group-item list-group-item-action list-group-item-dark"">{1}</span>" + "\n", Path.GetFileName(s), Path.GetFileName(s).Replace(".txt", ""));
            }

            html = html.Replace("{body}", sb.ToString());

            return html;
        }

        private string ViewCrashlog(HttpListenerRequest request)
        {
            string html = "";
            string crashlogName = GetPostBody(request);

            if (!string.IsNullOrEmpty(crashlogName))
            {
                string[] values = crashlogName.Split('=');

                try
                {
                    html = File.ReadAllText(Path.Combine(ConfigHelper._rootPath, "crashlogs", values[1]));
                }
                catch { }
            }

            return html;
        }

        private static string SelectVideoPage()
        {
            string html = GetHtml("selectvideo");

            string[] files = Directory.GetFiles(Path.Combine(ConfigHelper._rootPath, "windtracks"));
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

        private string GetCurrentMovie()
        {
            string moviename = "No video currently playing";
            if (_HTFanCtrl._settings.MediaPlayerType == "Audio")
            {
                moviename = "No video currently selected";
            }
            if (!string.IsNullOrEmpty(_HTFanCtrl._loadedVideoFilename))
            {
                moviename = $"Loaded Video: {_HTFanCtrl._loadedVideoFilename}";

                if (_HTFanCtrl._settings.MediaPlayerType != "Audio")
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

        private void FanCmd(HttpListenerRequest request)
        {
            string fanCmdInfo = GetPostBody(request);

            _HTFanCtrl._log.LogMsg($"Sent CMD: {fanCmdInfo}");
            _HTFanCtrl._fanController.SendCMD(fanCmdInfo);
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

            using FileStream output = new FileStream(filePath, FileMode.Create, FileAccess.Write);
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
            len -= startPos;

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