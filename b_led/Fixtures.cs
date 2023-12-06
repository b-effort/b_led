using System.Net;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;
using vtortola.WebSockets.Rfc6455;

namespace b_effort.b_led; 

// TODO: simplify, use HttpListener directly
// https://github.com/paulbatum/WebSocket-Samples/blob/master/HttpListenerWebSocketEcho/Server/Server.cs
static class FixtureServer {
	enum MessageType : byte {
		GetId = 0,
		GetId_Reply = 1,
		SetLEDs = 2,
	}
	
	const int Port = 42000;
	static readonly WebSocketListener ws;
	static readonly Dictionary<string, WebSocket> clients = new();

	const int BufferSize = State.BufferWidth * State.BufferWidth * 3 + 1;
	static byte[] ledBuffer = new byte[BufferSize];

	static FixtureServer() {
		var endPoint = new IPEndPoint(IPAddress.Any, Port);
		var options = new WebSocketListenerOptions {
			Standards = { new WebSocketFactoryRfc6455() },
			SendBufferSize = BufferSize,
			PingMode = PingMode.Manual,
			// Logger = ConsoleLogger.Instance,
		};
		ws = new WebSocketListener(endPoint, options);
	}

	public static async void Start() {
		await ws.StartAsync();
		_ = Task.Run(AcceptClients);
		// _ = Task.Run(
		// 	async () => {
		// 		foreach ((_, WebSocket client) in clients) {
		// 			if (client.IsConnected) {
		// 				await client.ReadMessageAsync(CancellationToken.None);
		// 			}
		// 		}
		// 	}
		// );
	}

	public static async Task AcceptClients() {
		while (ws.IsStarted) {
			try {
				WebSocket? client = await ws.AcceptWebSocketAsync(CancellationToken.None);
				if (client?.IsConnected != true)
					continue;
				Console.WriteLine($"Client connected: {client.RemoteEndpoint.Serialize()}");
					
				await client.WriteBytesAsync(new[] { (byte)MessageType.GetId });
				string? fixtureId = await client.ReadStringAsync(CancellationToken.None);
				if (fixtureId != null) {
					fixtureId = fixtureId[1..];
					Console.WriteLine($"Fixture ID: {fixtureId}");
					clients[fixtureId] = client;
				}
			} catch (Exception ex) {
				Console.WriteLine($"ERROR: Failed to accept client {ex}");
			}
		}
	}

	static Task? sendTask = null;
	
	public static async Task SendLEDs() {
		if (sendTask != null && sendTask.IsCompleted != true) {
			Console.WriteLine($"still sending");
			await sendTask;
		}
		HSB[,] inputs = State.previewBuffer;
		ledBuffer[0] = (byte)MessageType.SetLEDs;

		for (var y = 0; y < State.BufferWidth; y++) {
			for (var x = 0; x < State.BufferWidth; x++) {
				var rgb = inputs[y, x].ToRGB();
				var i = (y * State.BufferWidth + x) * 3 + 1;
				ledBuffer[i + 0] = rgb.r;
				ledBuffer[i + 1] = rgb.g;
				ledBuffer[i + 2] = rgb.b;
			}
		}

		sendTask = Task.Run(SendAsync);
		return;

		async Task SendAsync() {
			foreach ((string fixtureId, WebSocket client) in clients) {
				if (client.IsConnected) {
					try {
						await client.WriteBytesAsync(ledBuffer);
					} catch (Exception ex) {
						Console.WriteLine(ex);
					}
				}
			}
		}
	}
}
