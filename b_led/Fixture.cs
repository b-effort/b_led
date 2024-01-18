using System.Buffers;
using System.Diagnostics.Tracing;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace b_effort.b_led;

record struct FixtureLEDMap(Vector2[] leds) {
	public readonly Vector2[] leds = leds;
}

[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature, ImplicitUseTargetFlags.WithInheritors)]
abstract class Fixture {
	public static readonly Fixture[] All = AppDomain.CurrentDomain.GetAssemblies()
		.SelectMany(a => a.GetTypes())
		.Where(t => t.IsSealed && typeof(Fixture).IsAssignableFrom(t))
		.Select(t => (Fixture)Activator.CreateInstance(t)!)
		.ToArray();
	
	public readonly string name;
	public readonly FixtureLEDMap ledMap;

	protected Fixture(string name, FixtureLEDMap ledMap) {
		this.name = name;
		this.ledMap = ledMap;
	}
}

// I could make a separate FixtureManager ...but I hate that
// anyways, greg's more than up for the task
static partial class Greg {
	public static Fixture[] Fixtures { get; set; } = Array.Empty<Fixture>();
}

static class FixtureServer {
	enum MessageType : byte {
		GetId = 0,
		GetId_Reply = 1,
		SetLEDs = 2,
	}

	const string Address = "http://+:42000/b_led/";
	static readonly HttpListener httpListener = new();
	static readonly Dictionary<string, WebSocket> clients = new();
	
	const int FPS = 60;
	const float FrameTimeTarget = 1f / FPS;
	static float frameTime = 0f;

	static Task? acceptClientsTask;
	static Task? sendTask;
	static readonly AutoResetEvent sendFrameEvent = new(false);

	public static void Start() {
		httpListener.Prefixes.Add(Address);
		httpListener.Start();
		
		acceptClientsTask = Task.Run(LoopAcceptClients);
		sendTask = Task.Run(LoopSend);
	}

	public static void Update(float deltaTime) {
		frameTime += deltaTime;
		if (frameTime >= FrameTimeTarget) {
			frameTime -= FrameTimeTarget;
			sendFrameEvent.Set();
		}
	}
	
	static async Task LoopAcceptClients() {
		while (httpListener.IsListening) {
			var ctx = await httpListener.GetContextAsync();
			
			if (!ctx.Request.IsWebSocketRequest) {
				ctx.Response.StatusCode = 400;
				ctx.Response.Close();
				continue;
			}
			
			try {
				var wsCtx = await ctx.AcceptWebSocketAsync(subProtocol: null);
				var client = wsCtx.WebSocket;
				Console.WriteLine($"Client connected: {ctx.Request.RemoteEndPoint.Serialize()}");

				await client.SendAsync(new[] { (byte)MessageType.GetId }, WebSocketMessageType.Binary, true, CancellationToken.None);
				
				byte[] idBuffer = ArrayPool<byte>.Shared.Rent(128);
				var idResponse = await client.ReceiveAsync(idBuffer, CancellationToken.None);
				string id = Encoding.UTF8.GetString(idBuffer, 1, idResponse.Count - 1);
				Console.WriteLine($"Fixture ID: {id}");
				
				clients[id] = client;
				
			} catch (Exception ex) {
				Console.WriteLine($"ERROR: Failed to accept client {ex}");
				ctx.Response.StatusCode = 500;
				ctx.Response.Close();
			}
		}
		// ReSharper disable once FunctionNeverReturns
	}
	
	const int SendBufferSize = Greg.BufferWidth * Greg.BufferWidth * 3 + 1;
	static readonly byte[] sendBuffer = new byte[SendBufferSize];

	static async Task LoopSend() {
		while (httpListener.IsListening) {
			sendFrameEvent.WaitOne();

			RGB[,] inputs = Greg.outputBuffer;
			sendBuffer[0] = (byte)MessageType.SetLEDs;

			for (var y = 0; y < Greg.BufferWidth; y++) {
				for (var x = 0; x < Greg.BufferWidth; x++) {
					var color = inputs[y, x];
					var i = (y * Greg.BufferWidth + x) * 3 + 1;
					sendBuffer[i + 0] = color.r;
					sendBuffer[i + 1] = color.g;
					sendBuffer[i + 2] = color.b;
				}
			}

			foreach ((string fixtureId, WebSocket client) in clients) {
				if (client.State == WebSocketState.Open) {
					try {
						await client.SendAsync(sendBuffer, WebSocketMessageType.Binary, true, CancellationToken.None);
					} catch (Exception ex) {
						Console.WriteLine(ex);
					}
				}
			}
		}
		// ReSharper disable once FunctionNeverReturns
	}
}

sealed class DebugSocketEventListener : EventListener {
	protected override void OnEventSourceCreated(EventSource eventSource) {
		Console.WriteLine($"source {eventSource.Name}");
		if (
			eventSource.Name is "Private.InternalDiagnostics.System.Net.HttpListener" 
		                     or "Private.InternalDiagnostics.System.Net.Sockets"
		) {
			this.EnableEvents(eventSource, EventLevel.LogAlways);
		}
	}

	protected override void OnEventWritten(EventWrittenEventArgs e) {
		var sb = new StringBuilder()
			.Append($"{e.TimeStamp:HH:mm:ss.fffffff} [{e.EventName}] ");
		for (int i = 0; i < e.Payload?.Count; i++) {
			if (i > 0)
				sb.Append(", ");

			sb.Append($"{e.PayloadNames?[i]}: {e.Payload[i]}");
		}
		try {
			Console.WriteLine(sb.ToString());
		} catch { /**/ }
	}
}
