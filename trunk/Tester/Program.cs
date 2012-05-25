using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer;
using Org.Reddragonit.EmbeddedWebServer.Diagnostics;

namespace Tester
{
    class Program
    {
        static void Main(string[] args)
        {
            Logger.OverrideDiagnosticsLevel(DiagnosticsLevels.TRACE);
            Logger.OverrideOutputLevel(DiagnosticsOutputs.CONSOLE);
            ServerControl.Start();
            Console.WriteLine("Press enter to shut down...");
            Console.ReadLine();
            ServerControl.Stop();
        }
    }
}
