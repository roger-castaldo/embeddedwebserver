using System;
using System.Collections.Generic;
using System.Text;

namespace Org.Reddragonit.EmbeddedWebServer.Diagnostics
{
    //the available logging levels from no logging to logging every detail
    public enum DiagnosticsLevels
    {
        NONE = 0,
        SECURITY = 1,
        CRITICAL = 2,
        DEBUG = 3,
        TRACE = 4
    }

    //the available logging output types
    public enum DiagnosticsOutputs
    {
        DEBUG,
        CONSOLE,
        FILE,
        SOCKET
    }
}
