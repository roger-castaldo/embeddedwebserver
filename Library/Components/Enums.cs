using System;
using System.Collections.Generic;
using System.Text;

namespace Org.Reddragonit.EmbeddedWebServer.Components
{
    //the available session storage types
    public enum SiteSessionTypes
    {
        None,
        ThreadState,
        FileSystem
    }

    //response codes written into basic english, including their numbers
    public enum HttpStatusCodes
    {
        Continue = 100,
        OK = 200,
        Moved_Permanently = 301,
        Found = 302,
        Not_Modified=304,
        Temporary_Redirect=307,
        Bad_Request = 400,
        Unauthorized = 401,
        Forbidden = 403,
        Not_Found = 404,
        Method_Not_Allowed = 405,
        Not_Acceptable = 406,
        Request_Timeout = 408,
        Request_Entity_Too_Large = 413,
        RequestURI_Too_Long = 414,
        Unsupported_Media_Type = 415,
        Internal_Server_Error = 500,
        Not_Implemented = 501,
        Bad_Gateway = 502,
        Service_Unavailable = 503,
        HTTP_Version_Not_Supported = 505
    }

    public enum BrowserOSTypes
    {
        Linux,
        Windows,
        MAC,
        Other,
        Bot,
        BlackBerry
    }

    public enum BrowserFamilies
    {
        Bot,
        LotusNotes,
        Opera,
        InternetExplorer,
        Gecko,
        Camino,
        Chimera,
        Firebird,
        Phoenix,
        Galeon,
        Firefox,
        Netscape,
        Chrome,
        Safari,
        Konqueror,
        NetFront,
        BlackBerry,
        Other
    }

    public enum BotTypes
    {
        Yahoo,
        Google,
        MSNBot,
        WebCrawler,
        Inktomi,
        Teoma,
        Other
    }
}
