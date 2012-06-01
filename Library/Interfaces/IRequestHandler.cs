using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Components;
using Org.Reddragonit.EmbeddedWebServer.Components.Message;

namespace Org.Reddragonit.EmbeddedWebServer.Interfaces
{
    /*
     * This interface is used to implement a custom request handler that can 
     * be used by a given site.
     */
    public interface IRequestHandler
    {
        //returns whether or not the handler needs to be recreated per request
        //WARNING: when recreated the Init and DeInit will be called for every request
        bool IsReusable { get; }
        
        //called to see if the handler is valid for the given request
        bool CanProcessRequest(HttpRequest request, Site site);
        //called to process the given request after it was determined to be the appropriate handler
        void ProcessRequest(HttpRequest request, Site site);
        //any initialization functionality that should occur when the site starts
        void Init();
        //any shutdown functionality that should occur when the site shutsdown
        void DeInit();
        //returns whether or not the current request requires a session
        bool RequiresSessionForRequest(HttpRequest request, Site site);
    }
}
