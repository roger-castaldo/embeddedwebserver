using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using System.Threading;
using Org.Reddragonit.EmbeddedWebServer.Attributes;
using System.IO;

namespace Org.Reddragonit.EmbeddedWebServer.Diagnostics
{
    public class Logger : IBackgroundOperationContainer
    {
        private const int MESSAGE_WRITE_COUNT = 20;
        private static object _lock = new object();
        private static Queue<string> _messages = new Queue<string>();

        [BackgroundOperationCall(-1, -1, -1, -1, BackgroundOperationDaysOfWeek.All)]
        internal static void ProcessMessageQueue()
        {
            Monitor.Enter(_lock);
            if (_messages.Count > 0)
            {
                if (!new DirectoryInfo(Settings.LogPath).Exists)
                {
                    string cur = "";
                    foreach (string str in Settings.LogPath.Split(Path.DirectorySeparatorChar))
                    {
                        cur += str;
                        if (!new DirectoryInfo(cur).Exists)
                            new DirectoryInfo(cur).Create();
                        cur += Path.DirectorySeparatorChar;
                    }
                }
                if (!new FileInfo(Settings.LogPath + Path.DirectorySeparatorChar + DateTime.Now.ToString("yyyy-MM-dd") + ".txt").Exists)
                    new FileInfo(Settings.LogPath + Path.DirectorySeparatorChar + DateTime.Now.ToString("yyyy-MM-dd") + ".txt").Create();
                StreamWriter sw = new StreamWriter(new FileStream(Settings.LogPath + Path.DirectorySeparatorChar + DateTime.Now.ToString("yyyy-MM-dd") + ".txt", FileMode.Append, FileAccess.Write, FileShare.Read));
                for (int x = 0; x < MESSAGE_WRITE_COUNT; x++)
                {
                    if (_messages.Count == 0)
                        break;
                    sw.WriteLine(_messages.Dequeue());
                }
            }
            Monitor.Exit(_lock);
        }

        internal static void CleanupRemainingMessages()
        {
            Monitor.Enter(_lock);
            if (_messages.Count > 0)
            {
                if (!new DirectoryInfo(Settings.LogPath).Exists)
                {
                    string cur = "";
                    foreach (string str in Settings.LogPath.Split(Path.DirectorySeparatorChar))
                    {
                        cur += str;
                        if (!new DirectoryInfo(cur).Exists)
                            new DirectoryInfo(cur).Create();
                        cur += Path.DirectorySeparatorChar;
                    }
                }
                if (!new FileInfo(Settings.LogPath + Path.DirectorySeparatorChar + DateTime.Now.ToString("yyyy-MM-dd") + ".txt").Exists)
                    new FileInfo(Settings.LogPath + Path.DirectorySeparatorChar + DateTime.Now.ToString("yyyy-MM-dd") + ".txt").Create();
                StreamWriter sw = new StreamWriter(new FileStream(Settings.LogPath + Path.DirectorySeparatorChar + DateTime.Now.ToString("yyyy-MM-dd") + ".txt", FileMode.Append, FileAccess.Write, FileShare.Read));
                while (_messages.Count > 0)
                {
                    sw.WriteLine(_messages.Dequeue());
                }
            }
            Monitor.Exit(_lock);
        }

        public static void LogMessage(DiagnosticsLevels logLevel, string Message)
        {
            if (Site.CurrentSite != null)
            {
                if ((int)Site.CurrentSite.DiagnosticsLevel >= (int)logLevel)
                {
                    switch (Site.CurrentSite.DiagnosticsOutput)
                    {
                        case DiagnosticsOutputs.DEBUG:
                            System.Diagnostics.Debug.WriteLine(_FormatDiagnosticsMessage(Site.CurrentSite, logLevel, Message));
                            break;
                        case DiagnosticsOutputs.CONSOLE:
                            Console.WriteLine(_FormatDiagnosticsMessage(Site.CurrentSite, logLevel, Message));
                            break;
                        case DiagnosticsOutputs.FILE:
                            AppendMessageToFile(Site.CurrentSite, logLevel, Message);
                            break;
                    }
                }
            }
            else
            {
                if ((int)Settings.DiagnosticsLevel >= (int)logLevel)
                {
                    switch (Settings.DiagnosticsOutput)
                    {
                        case DiagnosticsOutputs.DEBUG:
                            System.Diagnostics.Debug.WriteLine(_FormatDiagnosticsMessage(null, logLevel, Message));
                            break;
                        case DiagnosticsOutputs.CONSOLE:
                            Console.WriteLine(_FormatDiagnosticsMessage(null, logLevel, Message));
                            break;
                        case DiagnosticsOutputs.FILE:
                            AppendMessageToFile(null, logLevel, Message);
                            break;
                    }
                }
            }
        }

        private static void AppendMessageToFile(Site site, DiagnosticsLevels logLevel, string Message)
        {
            Monitor.Enter(_lock);
            _messages.Enqueue(_FormatDiagnosticsMessage(site, logLevel, Message));
            Monitor.Exit(_lock);
        }

        private static string _FormatDiagnosticsMessage(Site site, DiagnosticsLevels logLevel, string Message)
        {
            if (site != null)
            {
                if (site.ServerName != null)
                    return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "|" + logLevel.ToString() + "|" + site.ServerName + "|" + Message;
                else
                    return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "|" + logLevel.ToString() + "|" + site.IPToListenTo.ToString() + ":" + Site.CurrentSite.Port.ToString() + "|" + Message;
            }
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "|" + logLevel.ToString() + "|null|" + Message;
        }
    }
}
