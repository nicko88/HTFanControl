using System;
using System.Diagnostics;
using System.IO;

namespace HTFanControl
{
    public static class Log
    {
        private static readonly string _path = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), $"{DateTime.Now:MM.dd.yy-hh.mm-tt}_log.txt");
        public static void WriteLine(string line)
        {
            File.AppendAllText(_path, line + Environment.NewLine);
        }
    }
}