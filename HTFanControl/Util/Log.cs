using System;
using System.Collections.Generic;
using System.IO;

namespace HTFanControl.Util
{
    public class Log
    {
        private static readonly string _logPath = Path.Combine(ConfigHelper._rootPath, "eventlog.txt");
        private static readonly string _traceLogPath = Path.Combine(ConfigHelper._rootPath, "tracelog.txt");
        private static bool _traceLogEnabed = false;

        public LinkedList<string> RealtimeLog { get; } = new LinkedList<string>();

        public Log()
        {
            try
            {
                File.Delete(_logPath);
            }
            catch { }

            if(File.Exists(_traceLogPath))
            {
                _traceLogEnabed = true;
            }
        }

        public void LogMsg(string line)
        {
            string timestamp = $"[{DateTime.Now:hh:mm:ss.fff}]: ";

            Console.WriteLine($"{timestamp}{line}");

            try
            {
                File.AppendAllText(_logPath, $"{timestamp}{line}{Environment.NewLine}");
            }
            catch { }

            RealtimeLog.AddFirst($"{timestamp}{line}");
            if(RealtimeLog.Count > 50)
            {
                RealtimeLog.RemoveLast();
            }
        }

        public static void LogTrace(string line)
        {
            if (_traceLogEnabed)
            {
                string timestamp = $"[{DateTime.Now:hh:mm:ss.fff}]: ";

                try
                {
                    File.AppendAllText(_traceLogPath, $"{timestamp}{line}{Environment.NewLine}");
                }
                catch { }
            }
        }
    }
}