using Microsoft.AspNet.SignalR.Client.Transports;
using System;
using Microsoft.AspNet.SignalR.Client;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Client.Http;
using Microsoft.AspNet.SignalR.Client.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Xamarin.Forms;

namespace XamarinChat.WebSocketImplementation
{
    public class WebSocketTransportLayer : IClientTransport
    {
        private IWebSocket _webSocket;
        private const ushort SuccessCloseStatus = 1000;
        private IConnection _connection;
        private string _connectionData;
        private CancellationToken _disconnectToken;
        private CancellationTokenSource _disconnectTokenSource;
        private bool _finished = false;
        private IHttpClient httpClient;
        private readonly TransportAbortHandler _abortHandler;
      
        public WebSocketTransportLayer(IHttpClient httpClient)
        {
            this.httpClient = httpClient;
            _abortHandler = new TransportAbortHandler(httpClient, Name);
            ReconnectDelay = TimeSpan.FromSeconds(5);
        }
        /// <summary>
        /// The time to wait after a connection drops to try reconnecting.
        /// </summary>
        public TimeSpan ReconnectDelay { get; set; }

        public bool SupportsKeepAlive
        {
            get { return true; }
        }

        public string Name { get { return "webSockets"; } }


        protected internal void TransportFailed(Exception ex)
        {
            // will be no-op if handler already finished (either succeeded or failed)            
        }
        public async Task Start(IConnection connection, string connectionData, CancellationToken disconnectToken)
        {
            _connection = connection;
            _connectionData = connectionData;
            _disconnectToken = disconnectToken;
            await Start(connection, connectionData);
        }
        private async Task Start(IConnection connection, string connectionData)
        {
            try
            {
                await StartWebSocket(connection, UrlBuilder.BuildConnect(connection, Name, connectionData));
            }
            catch (TaskCanceledException)
            {
                TransportFailed(null);
            }
            catch (Exception ex)
            {
                TransportFailed(ex);
            }
        }

        protected void OnStartFailed()
        {
            // if the transport failed to start we want to stop it silently.
            Dispose();
        }

        private async Task StartWebSocket(IConnection connection, string url)
        {
            var uri = UrlBuilder.ConvertToWebSocketUri(url);
            connection.Trace(TraceLevels.Events, "WS Connecting to: {0}", uri);

            if (_webSocket == null)
            {
                var holder = new ConnectionHolder(connection);

                _webSocket = DependencyService.Get<IWebSocket>();
                _webSocket.ResolveWebSocketResponse = MessageReceived;
                if (_disconnectTokenSource == null)
                {
                    _disconnectTokenSource = new CancellationTokenSource();
                }
                _webSocket.DisconectToken = _disconnectTokenSource.Token;//_disconnectTokenSource.Token;
                _webSocket.Connection = holder;
                connection.PrepareRequest(new WebSocketRequest(_webSocket, connection));
                await OpenWebSocket(_webSocket, uri);
            }
        }

        // testing/mocking
        protected virtual async Task OpenWebSocket(IWebSocket webSocket, Uri uri)
        {
            CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disconnectTokenSource.Token, _disconnectToken);
            CancellationToken token = linkedCts.Token;
            await webSocket.ConnectAsync(uri, token);
            await Task.Delay(ReconnectDelay);
        }
        internal void MessageReceived(string response)
        {
            _connection.Trace(TraceLevels.Messages, "WS: OnMessage({0})", response);

            ProcessResponse(_connection, response);
        }

        public Task Send(IConnection connection, string data, string connectionData)
        {
            if (connection == null)
            {
                throw new ArgumentNullException("connection");
            }

            var webSocket = _webSocket;

            if (webSocket == null)
            {
                Exception ex;
                if (connection.State != ConnectionState.Disconnected)
                {
                    // Make this a faulted task and trigger the OnError even to maintain consistency with the HttpBasedTransports
                    ex = new InvalidOperationException("Error Data Cannot Be Sent During Web Socket Reconnect");
                    connection.OnError(ex);
                }
                else
                {
                    ex = new InvalidOperationException("Data Cannot Be Sent During WebSocket Reconnect");
                }

                var tcs = new TaskCompletionSource<object>();
                tcs.SetException(ex);
                return tcs.Task;
            }

            return Send(webSocket, data);
        }

        // internal for testing
        internal static async Task Send(IWebSocket webSocket, string data)
        {
            await webSocket.SendMessageAsync(data);
        }

        // internal for testing
        internal async Task Reconnect(IConnection connection, string connectionData)
        {
            var reconnectUrl = UrlBuilder.BuildReconnect(connection, Name, connectionData);
            if (ConnectionState.Disconnected == connection.State || !_webSocket.IsConnected)
            {
                while (TransportHelper.VerifyLastActive(connection) && connection.EnsureReconnecting() && !_disconnectToken.IsCancellationRequested)
                {
                    try
                    {
                        await StartWebSocket(connection, reconnectUrl);

                        if (connection.ChangeState(ConnectionState.Reconnecting, ConnectionState.Connected))
                        {
                            connection.OnReconnected();
                        }

                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        connection.OnError(ex);
                    }
                    await Task.Delay(ReconnectDelay);
                }
            }
        }

        public void LostConnection(IConnection connection)
        {
            if (connection == null)
            {
                throw new ArgumentNullException("connection");
            }

            connection.Trace(TraceLevels.Events, "WS: LostConnection");
            Task.Run(() => Reconnect(_connection, _connectionData));
        }

        private void DisposeSocket()
        {
            _finished = true;
            _disconnectTokenSource.Cancel();
            _disconnectTokenSource = null;
            var webSocket = Interlocked.Exchange(ref _webSocket, null);
            if (webSocket != null)
            {
                webSocket.DisposeSocket();
                webSocket = null;
            }
        }

        public async Task<NegotiationResponse> Negotiate(IConnection connection, string connectionData)
        {
            if (_finished)
            {
                throw new InvalidOperationException("Error_TransportCannotBeReused");
            }
            if (httpClient == null)
            {
                throw new ArgumentNullException("httpClient");
            }

            if (connection == null)
            {
                throw new ArgumentNullException("connection");
            }

            var negotiateUrl = UrlBuilder.BuildNegotiate(connection, connectionData);

            httpClient.Initialize(connection);
            var response = await httpClient.Get(negotiateUrl, connection.PrepareRequest, isLongRunning: false);
            if (response == null)
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, "Error_ServerNegotiationFailed"));
            }
            else
            {
                var result = await response.ReadAsString();
                if (string.IsNullOrEmpty(result))
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, "Error_ServerNegotiationFailed"));
                }

                return JsonConvert.DeserializeObject<NegotiationResponse>(result);
            }
        }

        public void Abort(IConnection connection, TimeSpan timeout, string connectionData)
        {
            _finished = true;
           // DisposeSocket();
            _abortHandler.Abort(connection, timeout, connectionData);
        }
        public virtual async Task<NegotiationResponse> GetNegotiationResponse(IHttpClient httpClient, IConnection connection, string connectionData)
        {
            if (httpClient == null)
            {
                throw new ArgumentNullException("httpClient");
            }

            if (connection == null)
            {
                throw new ArgumentNullException("connection");
            }

            var negotiateUrl = UrlBuilder.BuildNegotiate(connection, connectionData);

            httpClient.Initialize(connection);
            var response = await httpClient.Get(negotiateUrl, connection.PrepareRequest, isLongRunning: false);
            if (response == null)
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, "Error_ServerNegotiationFailed"));
            }
            else
            {
                var result = await response.ReadAsString();
                if (string.IsNullOrEmpty(result))
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, "Error_ServerNegotiationFailed"));
                }

                return JsonConvert.DeserializeObject<NegotiationResponse>(result);
            }
        }

        // virtual to allow mocking
        public virtual async Task<string> GetStartResponse(IHttpClient httpClient, IConnection connection, string connectionData, string transport)
        {
            if (httpClient == null)
            {
                throw new ArgumentNullException("httpClient");
            }

            if (connection == null)
            {
                throw new ArgumentNullException("connection");
            }

            var startUrl = UrlBuilder.BuildStart(connection, transport, connectionData);
            var response = await httpClient.Get(startUrl, connection.PrepareRequest, isLongRunning: false);
            if (response == null)
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, "Error_ServerNegotiationFailed"));
            }
            else
            {
                var result = await response.ReadAsString();
                if (string.IsNullOrEmpty(result))
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, "Error_ServerNegotiationFailed"));
                }

                return result;
            }
        }


        public static bool VerifyLastActive(IConnection connection)
        {
            if (connection == null)
            {
                throw new ArgumentNullException("connection");
            }
            // Ensure that we have not exceeded the reconnect window
            if (DateTime.UtcNow - connection.LastActiveAt >= connection.ReconnectWindow)
            {
                connection.Trace(TraceLevels.Events, "There has not been an active server connection for an extended period of time. Stopping connection.");
                connection.Stop(new TimeoutException(String.Format(CultureInfo.CurrentCulture, "Error_ReconnectWindowTimeout",
                    connection.LastActiveAt, connection.ReconnectWindow)));
                return false;
            }
            return true;
        }
      
        protected internal virtual bool ProcessResponse(IConnection connection, string response)
        {
            if (connection == null)
            {
                throw new ArgumentNullException("connection");
            }            
            connection.MarkLastMessage();
            if (String.IsNullOrEmpty(response))
            {
                return false;
            }

            var shouldReconnect = false;

            try
            {
                var result = connection.JsonDeserializeObject<JObject>(response);

                if (!result.HasValues)
                {
                    return false;
                }

                if (result["I"] != null)
                {
                    connection.OnReceived(result);
                    return false;
                }

                shouldReconnect = (int?)result["T"] == 1;

                var groupsToken = result["G"];
                if (groupsToken != null)
                {
                    connection.GroupsToken = (string)groupsToken;
                }

                var messages = result["M"] as JArray;
                if (messages != null)
                {
                    connection.MessageId = (string)result["C"];

                    foreach (JToken message in (IEnumerable<JToken>)messages)
                    {
                        connection.OnReceived(message);
                    }
                    if ((int?)result["S"] == 1)
                    {
                        var responseResult = GetStartResponse(httpClient, _connection, _connectionData, Name).Result;
                        if (responseResult == null)
                        {
                            throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, "Error_ServerNegotiationFailed"));
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(responseResult))
                            {
                                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, "Error_ServerNegotiationFailed"));
                            }
                            else
                            {
                                var started = _connection.JsonDeserializeObject<JObject>(responseResult)["Response"];
                                if (started.ToString() == "started")
                                {
                                    return shouldReconnect;
                                }
                                else
                                {
                                    OnStartFailed();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                connection.OnError(ex);
            }
            return shouldReconnect;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _finished = true;
                DisposeSocket();
            }
        }
    }
}