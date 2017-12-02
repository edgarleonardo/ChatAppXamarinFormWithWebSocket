using System.Collections.Generic;
using Microsoft.AspNet.SignalR.Client.Http;
using System.Net;
using Microsoft.AspNet.SignalR.Client;

namespace XamarinChat.WebSocketImplementation
{
    internal class WebSocketRequest : IRequest
    {
        private readonly IWebSocket _webSocket;
        private readonly IConnection Connection;
        public WebSocketRequest(IWebSocket webSocket, IConnection connection)
        {
            _webSocket = webSocket;
            this.Connection = connection;
            PrepareRequest();
        }

        public string UserAgent
        {
            get;
            set;
        }

        public string Accept
        {
            get;
            set;
        }

        public void Abort()
        {
        }
        public CookieContainer CookieContainer
        {
            get { return _webSocket.CookiesContainer; }
            set { _webSocket.CookiesContainer = value; }
        }
        public void SetRequestHeaders(IDictionary<string, string> headers)
        {
            if (headers == null)
            {
                throw new System.Exception("Header Empty");
            }
            foreach (var header in headers)
            {
                _webSocket.AddHeader(header.Key, header.Value);
            }
        }
        private void PrepareRequest()
        {
            if (Connection.CookieContainer != null)
            {
                CookieContainer = Connection.CookieContainer;
            }
        }
    }
}