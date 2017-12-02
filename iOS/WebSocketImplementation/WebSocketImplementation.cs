using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Threading;
using System.Text;
using System.Collections.Generic;
using XamarinChat.WebSocketImplementation;
using System.Net;

[assembly: Xamarin.Forms.Dependency(typeof(WebSocketImplementation))]
namespace XamarinChat.WebSocketImplementation
{
    public class WebSocketImplementation : IWebSocket
    {
        public ClientWebSocket WebSocket { get; set; }
        public Queue<string> QueueOfSends { get; set; }
        public CancellationToken DisconectToken { get; set; }
        public Action<string> ResolveWebSocketResponse { get; set; }
        public IConnectionHolder Connection { get; set; }
        public CookieContainer CookiesContainer
        { get { return WebSocket.Options.Cookies; } set { WebSocket.Options.Cookies = value; } }
        public WebSocketImplementation()
        {
            WebSocket = new ClientWebSocket();
            QueueOfSends = new Queue<string>();
        }
        public async Task ConnectAsync(Uri uri, CancellationToken _disconnectToken)
        {
            try
            {
                await Task.Factory.StartNew(async () =>
                {
                    var uniInfo = uri;
                    var disconnectToken = _disconnectToken;
                    while (true)
                    {
                        if (IsConnected)
                        {
                            await ReadMessage();
                            await SendMessage();
                        }
                        else
                        {
                            await WebSocket.ConnectAsync(uniInfo, disconnectToken);
                        }
                    }
                }, _disconnectToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                var mes = ex.Message;
            }
        }
        private async Task SendMessage()
        {
            try
            {
                do
                {
                    if (WebSocket.State != WebSocketState.Open)
                    {
                        // Make this a faulted task and trigger the OnError even to maintain consistency with the HttpBasedTransports
                        var ex = new InvalidOperationException("Error_DataCannotBeSentDuringWebSocketReconnect");
                        Connection.OnError(ex);
                        return;
                    }
                    var message = "";
                    QueueOfSends.TryDequeue(out message);
                    if (string.IsNullOrEmpty(message) && !CanSendMessage(message))
                        return;

                    var byteMessage = Encoding.UTF8.GetBytes(message);
                    var segmnet = new ArraySegment<byte>(byteMessage);

                    await WebSocket.SendAsync(segmnet, WebSocketMessageType.Text, true, DisconectToken);
                } while (QueueOfSends.Count > 0);
            }
            catch (Exception ex)
            {
                var men = ex.Message;
            }
        }
        private async Task ReadMessage()
        {
            try
            {
                WebSocketReceiveResult result;
                string receivedMessage = "";
                var message = new ArraySegment<byte>(new byte[4096]);
                do
                {
                    result = await WebSocket.ReceiveAsync(message, DisconectToken);
                    if (result.MessageType != WebSocketMessageType.Text)
                        break;
                    var messageBytes = message.Skip(message.Offset).Take(result.Count).ToArray();
                    receivedMessage += Encoding.UTF8.GetString(messageBytes);
                }
                while (!result.EndOfMessage);
                if (receivedMessage != "{}" && !string.IsNullOrEmpty(receivedMessage))
                {
                    ResolveWebSocketResponse.Invoke(receivedMessage);
                    Console.WriteLine("Received: {0}", receivedMessage);
                }
            }
            catch (Exception ex)
            {
                var mes = ex.Message;
            }
        }
        public Task SetRequestHeader(string key, string value)
        {
            //Connection.Headers.Add(key, value);
            Connection.AddHeader(key, value);
            return Task.FromResult(0);
        }
        bool CanSendMessage(string message)
        {
            return IsConnected && !string.IsNullOrEmpty(message);
        }
        public bool IsConnected => WebSocket.State == WebSocketState.Open;
        public bool IsAborted => (WebSocket.State == WebSocketState.Aborted || WebSocket.State == WebSocketState.Closed);
        public Task SendMessageAsync(string message)
        {
            QueueOfSends.Enqueue(message);
            return Task.FromResult(0);
        }

        public void DisposeSocket()
        {
            //ResolveWebSocketResponse = null;
        }

        public void AddHeader(string key, string value)
        {
            WebSocket.Options.SetRequestHeader(key, value);
        }
    }
}