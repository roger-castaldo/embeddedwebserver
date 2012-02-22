using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer;

namespace Tester
{
    class Program
    {
        static void Main(string[] args)
        {
            ServerControl.Start();
            Console.WriteLine("Press enter to shut down...");
            Console.ReadLine();
            ServerControl.Stop();
        }
    }
}
