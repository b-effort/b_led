using System.Buffers;
using System.Diagnostics.Tracing;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using static Raylib_cs.Rlgl;

namespace b_effort.b_led;

/* !plan
- fixture owns buffer and led map
- greg comps buffers into full preview
- pattern renders directly to fixture buffer
	- crop render x,y when pattern spans multiple fixtures
- pattern has separate preview buffer
- clip specifies which fixture groups to play on
 */

/*
https://electromage.com/docs/intro-to-mapping
1u = 1cm
([0, 1]], [0, 1])
 */

delegate void LEDMapper(Vector2[] coords);

[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature, ImplicitUseTargetFlags.WithInheritors)]
abstract class FixtureTemplate {
	public static readonly FixtureTemplate[] All = AppDomain.CurrentDomain.GetAssemblies()
		.SelectMany(a => a.GetTypes())
		.Where(t => t.IsSealed && typeof(FixtureTemplate).IsAssignableFrom(t))
		.Select(t => (FixtureTemplate)Activator.CreateInstance(t)!)
		.ToArray();

	public static readonly FixtureTemplate Default = All.First(t => t is Fixture_Default);

	public static FixtureTemplate FromId(Guid id) => All.First(t => t.Id == id);

	public Guid Id { get; }

	public readonly string name;
	public readonly LEDMapper mapper;

	protected FixtureTemplate(Guid id, LEDMapper mapper) {
		this.Id = id;
		this.mapper = mapper;
		this.name = this.GetDerivedNameFromType(trimPrefix: "Fixture");
	}
}

[DataContract]
sealed class Fixture {
	public const int Name_MaxLength = 64;
	public const int NetworkId_MaxLength = 64;

	[DataMember] public Guid Id { get; }
	[DataMember] public string name;
	[DataMember] Guid templateId;
	[DataMember] public List<string> groups;
	[DataMember] public string networkId;
	[DataMember] public int numLeds;
	// for fixtures with multiple sub-fixtures
	[DataMember] public int startingLedOffset;
	[DataMember] public Vector2 anchorPoint;
	[DataMember] public Vector2 worldPos;

	FixtureTemplate template;
	public FixtureTemplate Template {
		get => this.template;
		set {
			this.template = value;
			this.templateId = value.Id;
			this.RebuildMap();
			this.preview.UpdateMapping();
		}
	}

	RGB[] leds;
	Vector2[] coords;
	public Vector2 Bounds { get; private set; }

	readonly FixturePreview preview;
	public Texture2D PreviewTexture => this.preview.Texture;

	[JsonConstructor]
	public Fixture(
		Guid id,
		string name,
		Guid template_id,
		List<string>? groups = null,
		string network_id = "",
		int num_leds = 0,
		int starting_led_offset = 0,
		Vector2 anchor_point = default,
		Vector2 world_pos = default
	) {
		this.Id = id;
		this.name = name;
		this.templateId = template_id;
		this.groups = groups ?? new List<string>();
		this.networkId = network_id;
		this.numLeds = num_leds;
		this.startingLedOffset = starting_led_offset;
		this.anchorPoint = anchor_point;
		this.worldPos = world_pos;

		this.template = FixtureTemplate.FromId(template_id);
		this.leds = new RGB[num_leds];
		this.coords = new Vector2[num_leds];
		this.RebuildMap();

		this.preview = new FixturePreview(this.leds, this.coords);
	}

	public Fixture(string name = "") : this(
		id: Guid.NewGuid(),
		name,
		template_id: FixtureTemplate.Default.Id
	) { }

	public void Render(Pattern pattern, Palette? palette) =>
		pattern.RenderTo(this.leds, this.coords, this.Bounds, palette);

	public void Resize() {
		Array.Resize(ref this.leds, this.numLeds);
		Array.Resize(ref this.coords, this.numLeds);
		this.RebuildMap();
		this.preview.Resize(this.leds, this.coords);
	}

	void RebuildMap() {
		this.Template.mapper(this.coords);
		this.Bounds = GetBounds(this.coords);
	}

	public void UpdatePreview() => this.preview.RenderTexture();

	static Vector2 GetBounds(Vector2[] coords) {
		Vector2 max = Vector2.Zero;

		foreach (var value in coords) {
			if (value.X > max.X) max.X = value.X;
			if (value.Y > max.Y) max.Y = value.Y;
		}

		return max;
	}
}

sealed class FixturePreview : IDisposable {
	static int Width => (int)Config.PatternPreviewResolution.X;
	static int Height => (int)Config.PatternPreviewResolution.Y;

	RGB[] leds;
	Vector2[] coords;

	readonly RenderTexture2D rt;
	uint vao;
	uint vbo_leds;
	uint vbo_coords;

	int NumLeds => this.leds.Length;

	public Texture2D Texture => this.rt.texture;

	public FixturePreview(RGB[] leds, Vector2[] coords) {
		this.leds = leds;
		this.coords = coords;

		this.rt = rl.LoadRenderTexture(Width, Height);
		this.BuildVAO();
	}

	~FixturePreview() => this.Dispose();

	public void Dispose() {
		this.DisposeVAO();
		GC.SuppressFinalize(this);
	}

	unsafe void BuildVAO() {
		this.vao = rlLoadVertexArray();
		rlEnableVertexArray(this.vao);
		{
			this.vbo_leds = rlLoadVertexBuffer(Unsafe.AsPointer(ref this.leds), this.NumLeds * sizeof(RGB), true);
			rlSetVertexAttribute(0, 4, RL_UNSIGNED_BYTE, true, 0, (void*)0);
			rlEnableVertexAttribute(0);

			this.vbo_coords = rlLoadVertexBuffer(Unsafe.AsPointer(ref this.coords), this.NumLeds * sizeof(Vector2), true);
			rlSetVertexAttribute(1, 2, RL_FLOAT, false, 0, (void*)0);
			rlEnableVertexAttribute(1);

			rlDisableVertexBuffer();
		}
		rlDisableVertexBuffer();
	}

	void DisposeVAO() {
		rlUnloadVertexBuffer(this.vbo_leds);
		rlUnloadVertexBuffer(this.vbo_coords);
		rlUnloadVertexArray(this.vao);
		this.vbo_leds = 0;
		this.vbo_coords = 0;
		this.vao = 0;
	}

	public void Resize(RGB[] newLeds, Vector2[] newCoords) {
		this.leds = newLeds;
		this.coords = newCoords;
		this.DisposeVAO();
		this.BuildVAO();
	}

	public unsafe void UpdateMapping() {
		rlUpdateVertexBuffer(this.vbo_coords, Unsafe.AsPointer(ref this.coords), this.NumLeds * sizeof(Vector2), 0);
	}

	public unsafe void RenderTexture() {
		rlUpdateVertexBuffer(this.vbo_leds, Unsafe.AsPointer(ref this.leds), this.NumLeds * sizeof(RGB), 0);

		rl.BeginTextureMode(this.rt);
		{
			rl.ClearBackground(rlColor.BLACK);
			rl.BeginShaderMode(Shaders.FixturePreview);
			{
				rlEnableVertexArray(this.vao);
				{
					rlDrawVertexArray(0, this.NumLeds);
				}
				rlDisableVertexArray();
			}
			rl.EndShaderMode();
		}
		rl.EndTextureMode();
	}
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

				byte[] idBuffer = ArrayPool<byte>.Shared.Rent(Fixture.NetworkId_MaxLength * 2 + 1);
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
	}

	const int SendBufferSize = Greg.BufferWidth * Greg.BufferWidth * 3 + 1;
	static readonly byte[] sendBuffer = new byte[SendBufferSize];

	static async Task LoopSend() {
		while (httpListener.IsListening) {
			sendFrameEvent.WaitOne();

			// RGB[,] inputs = Greg.outputBuffer;
			sendBuffer[0] = (byte)MessageType.SetLEDs;

			for (var y = 0; y < Greg.BufferWidth; y++) {
				for (var x = 0; x < Greg.BufferWidth; x++) {
					// var color = inputs[y, x];
					RGB color = new RGB(0, 0, 0);

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
