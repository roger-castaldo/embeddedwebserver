using System;
using System.Collections.Generic;
using System.Text;

namespace Org.Reddragonit.EmbeddedWebServer.Components
{
    public enum SiteSessionTypes
    {
        None,
        ThreadState,
        FileSystem
    }

    public enum HttpStatusCodes
    {
        OK = 200,
        Moved_Permanently = 301,
        Found = 302,
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
        Internal_Server_Eror = 500,
        Not_Implemented = 501,
        Bad_Gateway = 502,
        Service_Unavailable = 503,
        HTTP_Version_Not_Supported = 505
    }
}
