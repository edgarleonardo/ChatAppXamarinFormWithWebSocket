using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net;

namespace XamarinChat.WebSocketImplementation
{
    public interface IWebSocket
    {
        bool IsConnected { get; }
        Queue<string> QueueOfSends { get; set; }
        CancellationToken DisconectToken { get; set; }
        Action<string> ResolveWebSocketResponse { get; set; }
        IConnectionHolder Connection { get; set; }
        Task SetRequestHeader(string Key, string Value);
        Task ConnectAsync(Uri uri, CancellationToken _disconnectToken);
        Task SendMessageAsync(string message);
        void DisposeSocket();
        void AddHeader(string key, string value);
        CookieContainer CookiesContainer { get; set; }
    }
}