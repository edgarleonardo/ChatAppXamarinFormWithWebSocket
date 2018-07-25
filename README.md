# Real-Time Chat App With Xamarin.Form And SignalR using WebSocket as transport method

This project shows how to use WebSocket to connect to **.NET Framework SignalR Server** using **Xamarin.Form**.

First Create the implementation on my PCL with these Classes and Interfaces:

**IWebSocke:** This is the interface that abstract the websocket behaviors from my PCL and the element used for the dependency inversion between PCL and each platform.

**IConnectionHolder:** this interface abstract the platform of several dependency needed only on PCL but not on platforms.

**ConnectionHolder:** Is the concrete implementation of IConnectionHolder interface and it has all logic needed at each platform. This class in injected on the Dependency Service of Xamarin.Form.

**WebSocketRequest:** This class is a concrete implementation of the interface IRequest, SignalR make a http negotiation before use any transport methodology and this object is the one used to establish the connection via HTTP for negotiation or for others transport method like Long Polling for example.

**WebSocketTransportLayer:** This class is a concrete implementation of the interface IClientTransport which is the interface used by the SignalR client to make the connections using the best transport methods the clients supports.

The second step was creating the websocket functionalities on each platform implementing the Interface that will be injected to abstract the platform behavior on the PCL.

**WebSocketImplementation:** This object is implemented on each platform IOS and Android and it implement the IWebSocket interface and is injected on the Dependency Service on Xamarin.Form like this **[assembly: Xamarin.Forms.Dependency(typeof(WebSocketImplementation))]** .
