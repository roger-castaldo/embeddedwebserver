using System;
using System.Collections.Generic;
using System.Text;

namespace Org.Reddragonit.EmbeddedWebServer.Diagnostics
{
    public enum DiagnosticsLevels
    {
        NONE = 0,
        CRITICAL = 1,
        DEBUG = 2,
        TRACE = 3
    }

    public enum DiagnosticsOutputs
    {
        DEBUG,
        CONSOLE,
        FILE
    }
}
