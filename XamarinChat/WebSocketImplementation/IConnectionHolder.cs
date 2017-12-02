using System;

namespace XamarinChat.WebSocketImplementation
{
    public interface IConnectionHolder
    {
        void OnError(Exception ex);
        void AddHeader(string key, string value);
    }
}