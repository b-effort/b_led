using System.Buffers;
using System.Diagnostics.Tracing;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace b_effort.b_led;

/*
https://electromage.com/docs/intro-to-mapping
1u = 1cm
([0, 1]], [0, 1])
 */

delegate LEDMap LEDMapper(Vector2[] leds);

readonly record struct LEDMap(Vector2[] leds) {
	public readonly Vector2[] leds = leds;
	public readonly Bounds bounds = GetBounds(leds);

	public int NumLeds => this.leds.Length;

	public static implicit operator LEDMap(Vector2[] value) => new(value);

	static Bounds GetBounds(Vector2[] values) {
		Vector2 min = new(float.PositiveInfinity), max = new(float.NegativeInfinity);

		foreach (var value in values) {
			if (value.X < min.X) min.X = value.X;
			if (value.Y < min.Y) min.Y = value.Y;
			if (value.X > max.X) max.X = value.X;
			if (value.Y > max.Y) max.Y = value.Y;
		}

		return new Bounds(min, max);
	}
}

[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature, ImplicitUseTargetFlags.WithInheritors)]
abstract class FixtureTemplate {
	public static readonly FixtureTemplate[] All = AppDomain.CurrentDomain.GetAssemblies()
		.SelectMany(a => a.GetTypes())
		.Where(t => t.IsSealed && typeof(FixtureTemplate).IsAssignableFrom(t))
		.Select(t => (FixtureTemplate)Activator.CreateInstance(t)!)
		.ToArray();
	
	public static FixtureTemplate FromId(Guid id) => All.First(f => f.Id == id);

	public abstract Guid Id { get; }

	public readonly string name;
	public readonly int numLeds;
	public readonly LEDMap ledMap;
	
	protected FixtureTemplate(int numLeds, LEDMapper mapper) {
		this.name = this.GetDerivedNameFromType();
		this.numLeds = numLeds;
		this.ledMap = mapper(new Vector2[numLeds]);
	}

	public Bounds Bounds => this.ledMap.bounds;
	public int NumLeds => this.ledMap.leds.Length;
}

[DataContract]
sealed class Fixture {
	public const int NameMaxLength = 64;

	[DataMember] public Guid Id { get; }
	[DataMember] public string name;

	public FixtureTemplate? template;
	[DataMember] public Guid? TemplateId {
		get => this.template?.Id;
		init {
			if (value.HasValue)
				this.template = FixtureTemplate.FromId(value.Value);
		}
	}

	public Fixture(string name) : this(
		id: Guid.NewGuid(),
		name,
		template_id: null
	) { }

	[JsonConstructor]
	public Fixture(Guid id, string name, Guid? template_id) {
		this.Id = id;
		this.name = name;
		this.TemplateId = template_id;
	}
	
	public bool HasTemplate => this.template != null;
	public Bounds? Bounds => this.template?.Bounds;
}

// I could make a separate FixtureManager ...but I hate that
// anyways, greg's more than up for the task
static partial class Greg {
	public static FixtureTemplate[] FixtureTemplates => FixtureTemplate.All;
	public static List<Fixture> Fixtures => project.Fixtures;
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
