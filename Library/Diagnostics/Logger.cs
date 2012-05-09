using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using System.Threading;
using Org.Reddragonit.EmbeddedWebServer.Attributes;
using System.IO;
using System.Net.Sockets;
using Org.Reddragonit.EmbeddedWebServer.Components;
using System.Diagnostics;

namespace Org.Reddragonit.EmbeddedWebServer.Diagnostics
{
    /*
     * This class houses all the logging functionality for the server.
     * For the file logging it queues up the data and then dumps it to the appropriate file
     * through a background operation that is called every minute.  When the 
     * logging is shut down by a server shutdown it finishes off this queue.
     */
    public class Logger : IBackgroundOperationContainer
    {
        //delegate used to append a message to the log file queue asynchronously
        private delegate void delAppendMessageToFile(Site site,HttpConnection conn, DiagnosticsLevels logLevel, string Message);

        //number of messages to write to a file with each pass of the background thread.
        private const int MESSAGE_WRITE_COUNT = 20;
        //object used to lock the message queue that houses file written messages
        private static object _lock = new object();
        //the queue to hold the messages to be written to a file
        private static Queue<string> _messages = new Queue<string>();

        //udp socket for remote logging
        private static Socket _sockLog = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        /*
         * The background thread function that gets called every minute.
         * It processes the queue for log messages that get written 
         * to files.  It will only process 20 messages at most each time 
         * in order to prevent too much locking time for adding new 
         * messages.
         */
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
                FileInfo fi = new FileInfo(Settings.LogPath + Path.DirectorySeparatorChar + DateTime.Now.ToString("yyyy-MM-dd") + ".txt");
                StreamWriter sw = new StreamWriter(fi.Open(FileMode.Append, FileAccess.Write, FileShare.Read));
                for (int x = 0; x < MESSAGE_WRITE_COUNT; x++)
                {
                    if (_messages.Count == 0)
                        break;
                    sw.WriteLine(_messages.Dequeue());
                }
                sw.Flush();
                sw.Close();
            }
            Monitor.Exit(_lock);
        }

        /*
         * Called when the server shuts down to process all the remaining messages in 
         * the file queue before shutting down the server completely.
         */
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

        /*
         * Called to log a message.  It checks if to check for site settings
         * for the log, or just general settings.  Once it has been determined if 
         * logging should occur, it then determines the type and either prints it
         * to the appropriate output, or it appends the message to the given queue asynchronously.
         */
        public static void LogMessage(DiagnosticsLevels logLevel, string Message)
        {
            if (Site.CurrentSite != null)
            {
                if ((int)Site.CurrentSite.DiagnosticsLevel >= (int)logLevel)
                {
                    switch (Site.CurrentSite.DiagnosticsOutput)
                    {
                        case DiagnosticsOutputs.DEBUG:
                            System.Diagnostics.Debug.WriteLine(_FormatDiagnosticsMessage(Site.CurrentSite,HttpConnection.CurrentConnection, logLevel, Message));
                            break;
                        case DiagnosticsOutputs.CONSOLE:
                            Console.WriteLine(_FormatDiagnosticsMessage(Site.CurrentSite, HttpConnection.CurrentConnection, logLevel, Message));
                            break;
                        case DiagnosticsOutputs.FILE:
                            new delAppendMessageToFile(AppendMessageToFile).BeginInvoke(Site.CurrentSite, HttpConnection.CurrentConnection, logLevel, Message, new AsyncCallback(QueueMessageComplete), null);
                            break;
                        case DiagnosticsOutputs.SOCKET:
                            _sockLog.SendTo(System.Text.ASCIIEncoding.ASCII.GetBytes(_FormatDiagnosticsMessage(Site.CurrentSite, HttpConnection.CurrentConnection, logLevel, Message) + "\n\n"), Site.CurrentSite.RemoteLoggingServer);
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
                            System.Diagnostics.Debug.WriteLine(_FormatDiagnosticsMessage(null, HttpConnection.CurrentConnection, logLevel, Message));
                            break;
                        case DiagnosticsOutputs.CONSOLE:
                            Console.WriteLine(_FormatDiagnosticsMessage(null, HttpConnection.CurrentConnection, logLevel, Message));
                            break;
                        case DiagnosticsOutputs.FILE:
                            new delAppendMessageToFile(AppendMessageToFile).BeginInvoke(null, HttpConnection.CurrentConnection, logLevel, Message, new AsyncCallback(QueueMessageComplete), null);
                            break;
                    }
                }
            }
        }

        //static function designed to catch finishing of async call to queue log message.
        private static void QueueMessageComplete(IAsyncResult res) { }

        private static void AppendMessageToFile(Site site,HttpConnection conn, DiagnosticsLevels logLevel, string Message)
        {
            Monitor.Enter(_lock);
            _messages.Enqueue(_FormatDiagnosticsMessage(site,conn, logLevel, Message));
            Monitor.Exit(_lock);
        }

        private const string _STACK_FRAME_FORMAT = "{0}:[{1}]";

        //formats a diagnostics message using the appropriate date time format as well as site and log level information
        private static string _FormatDiagnosticsMessage(Site site,HttpConnection conn, DiagnosticsLevels logLevel, string Message)
        {
            string sfs = "UNKNOWN";
            if (HttpConnection.CurrentConnection != null)
            {
                sfs = "HttpConnection[" + HttpConnection.CurrentConnection.ID.ToString() + "]";
            }
            else
            {
                try
                {
                    StackFrame sf = new StackFrame(2, true);
                    sfs = (sf.GetMethod() == null ? "UNKNOWN" : string.Format(_STACK_FRAME_FORMAT, new object[]{
                    sf.GetMethod().DeclaringType.FullName,
                    sf.GetMethod().Name
                }));
                }
                catch (Exception e) { }
            }
            if (site != null)
            {
                if (Settings.UseServerNameInLogging)
                {
                    if (site.ServerName != null)
                        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "|" + logLevel.ToString() + "|" + site.ServerName+"|"+sfs+"|" + Message;
                    else if (conn!=null)
                        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "|" + logLevel.ToString() + "|" + conn.Listener.Address.ToString() + ":" + conn.Listener.Port.ToString() + "|" + sfs + "|" + Message;
                    else
                        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "|" + logLevel.ToString() + "|" + site.ListenOn[0].Address.ToString() + ":" + site.ListenOn[0].Port.ToString() + "|" + sfs + "|" + Message;
                }else
                    return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "|" + logLevel.ToString() + "|" + sfs + "|" + Message;
            }
            else if (conn != null)
            {
                if (Settings.UseServerNameInLogging)
                    return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "|" + logLevel.ToString() + "|" + conn.Listener.Address.ToString() + ":" + conn.Listener.Port.ToString() + "|" + sfs + "|" + Message;
            }
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "|" + logLevel.ToString() + "|null|" + sfs + "|" + Message;
        }

        //called to log an error message, it traverses inner exceptions
        public static void LogError(Exception e)
        {
            Exception ex = e;
            while (ex != null)
            {
                LogMessage(DiagnosticsLevels.CRITICAL, ex.Message);
                LogMessage(DiagnosticsLevels.CRITICAL, ex.StackTrace);
                ex = ex.InnerException;
            }
        }
    }
}
