using System;
using System.Collections.Generic;
using System.Text;

namespace Org.Reddragonit.EmbeddedWebServer.Diagnostics
{
    //the available logging levels from no logging to logging every detail
    public enum DiagnosticsLevels
    {
        NONE = 0,
        CRITICAL = 1,
        DEBUG = 2,
        TRACE = 3
    }

    //the available logging output types
    public enum DiagnosticsOutputs
    {
        DEBUG,
        CONSOLE,
        FILE
    }
}
