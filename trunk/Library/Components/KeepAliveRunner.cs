using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using Org.Reddragonit.EmbeddedWebServer.Attributes;
using System.Net.Sockets;
using Org.Reddragonit.EmbeddedWebServer.Diagnostics;
using System.Net;
using System.IO;
using System.Threading;

namespace Org.Reddragonit.EmbeddedWebServer.Components
{
    internal class KeepAliveRunner : IBackgroundOperationContainer
    {
        [BackgroundOperationCall(0, -1, -1, -1, BackgroundOperationDaysOfWeek.All)]
        public static void PingTestSockets()
        {
            List<sIPPortPair> pairs = ServerControl.BoundPairs;
            foreach (sIPPortPair pair in pairs)
            {
                TcpClient sock = new TcpClient();
                try
                {
                    NetworkStream str;
                    byte[] buf;
                    if (!pair.Address.Equals(IPAddress.Any))
                    {
                        sock.Connect(pair.Address, pair.Port);
                        buf = System.Text.UTF8Encoding.UTF8.GetBytes("GET / HTTP/1.1\r\nHost: " + pair.Address.ToString() + ":" + pair.Port.ToString() + "\r\nConnection: Keep-alive\r\n\r\n");
                    }
                    else
                    {
                        sock.Connect(IPAddress.Loopback, pair.Port);
                        buf = System.Text.UTF8Encoding.UTF8.GetBytes("GET / HTTP/1.1\r\nHost: " + IPAddress.Loopback.ToString() + ":" + pair.Port.ToString() + "\r\nConnection: Keep-alive\r\n\r\n");
                    }
                    str = sock.GetStream();
                    str.Write(buf, 0, buf.Length);
                    str.Flush();
                    while (str.ReadByte() == -1)
                    {
                        Thread.Sleep(1);
                    }
                    sock.Close();
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                }
                try
                {
                    sock.Close();
                }
                catch (Exception e) { }
            }
        }

    }
}
