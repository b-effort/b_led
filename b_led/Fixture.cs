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
using b_effort.b_led.graphics;
using b_effort.b_led.resources;
using JetBrains.Annotations;
using OpenTK.Graphics.OpenGL4;

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
	readonly LEDMapper mapper;
	public readonly float ledSize;

	protected FixtureTemplate(Guid id, LEDMapper mapper, float ledSize) {
		this.Id = id;
		this.mapper = mapper;
		this.ledSize = ledSize;
		this.name = this.GetDerivedNameFromType(trimPrefix: "Fixture");
	}

	public void PopulateMap(Vector2[] coords) => this.mapper(coords);
}

[DataContract]
sealed class Fixture {
	public const int Name_MaxLength = 64;
	public const int NetworkId_MaxLength = 64;

	[DataMember] public Guid Id { get; }
	[DataMember] public string name;
	[DataMember] Guid templateId;
	[DataMember] public List<string> groups;
	// implicitly suffixed with .local
	[DataMember] public string hostname;
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
			this.preview.UpdateCoordsBuffer();
		}
	}

	RGBA[] leds;
	Vector2[] coords;
	public Vector2 Bounds { get; private set; }

	[JsonConstructor]
	public Fixture(
		Guid id,
		string name,
		Guid template_id,
		List<string>? groups = null,
		string hostname = "",
		int num_leds = 0,
		int starting_led_offset = 0,
		Vector2 anchor_point = default,
		Vector2 world_pos = default
	) {
		this.Id = id;
		this.name = name;
		this.templateId = template_id;
		this.groups = groups ?? new List<string>();
		this.hostname = hostname;
		this.numLeds = num_leds;
		this.startingLedOffset = starting_led_offset;
		this.anchorPoint = anchor_point;
		this.worldPos = world_pos;

		this.template = FixtureTemplate.FromId(template_id);
		this.leds = new RGBA[num_leds];
		this.coords = new Vector2[num_leds];
		this.RebuildMap();

		this.preview = new Preview(this);

		this.sendBuffer = new byte[this.SendBufferSize];
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
		Array.Resize(ref this.sendBuffer, this.SendBufferSize);
		this.RebuildMap();
		this.preview.Resize(this.numLeds);
	}

	void RebuildMap() {
		this.Template.PopulateMap(this.coords);
		this.Bounds = GetBounds(this.coords);
	}

#region ws

	readonly ClientWebSocket ws = new();
	Task? wsTask;
	CancellationTokenSource? wsCancelSource;
	byte[] sendBuffer;
	int SendBufferSize => this.numLeds * 3 + 1;

	public void Connect() {
		if (this.wsTask != null)
			throw new OopsiePoopsie($"Fixture {this.name} already connected");

		this.wsCancelSource = new CancellationTokenSource();
		this.wsTask = Task.Run(() => this.RunWS(this.wsCancelSource.Token));
	}

	public void Disconnect() {
		if (this.wsCancelSource is null)
			throw new OopsiePoopsie($"Fixture {this.name} isn't connected");

		this.wsCancelSource.Cancel();
		this.wsCancelSource.Dispose();
		this.wsCancelSource = null;
	}

	async Task RunWS(CancellationToken cancel) {
		var uri = new Uri($"{this.hostname}.local:{Config.WS_Port}/{Config.WS_Path}/");
		await this.ws.ConnectAsync(uri, CancellationToken.None);

		while (this.ws.State == WebSocketState.Open && !cancel.IsCancellationRequested) {

		}

		if (this.ws.State == WebSocketState.Open) {
			await this.ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
		}

		Console.WriteLine($"Fixture {this.hostname} disconnected: {this.ws.State}, cancelled: {cancel.IsCancellationRequested}");
	}

#endregion

#region preview

	readonly Preview preview;
	public Texture2D PreviewTexture => this.preview.Texture;

	public void UpdatePreview() => this.preview.UpdateTexture();

	static Vector2 GetBounds(Vector2[] coords) {
		Vector2 max = Vector2.Zero;

		foreach (var value in coords) {
			if (value.X > max.X) max.X = value.X;
			if (value.Y > max.Y) max.Y = value.Y;
		}

		return max;
	}

	sealed class Preview : IDisposable {
		static int Width => (int)Config.FixturePreviewResolution.X;
		static int Height => (int)Config.FixturePreviewResolution.Y;

		static Shader_FixturePreview Shader => Shaders.FixturePreview;

		readonly Fixture fixture;
		readonly RenderTexture2D rt;
		Matrix4 projection;
		int vao;
		int vbo_leds;
		int vbo_coords;

		int length;

		public Preview(Fixture fixture) {
			this.fixture = fixture;
			this.length = this.fixture.numLeds;

			this.rt = new RenderTexture2D(Width, Height);
			this.InitShader();
		}

		public Texture2D Texture => this.rt.texture;

		~Preview() => this.Dispose();

		public void Dispose() {
			this.UnloadVAO();
			GC.SuppressFinalize(this);
		}

		unsafe void InitShader() {
			gl.Enable(EnableCap.ProgramPointSize);

			this.projection = Matrix4.CreateScale(1 / this.fixture.Bounds.X, 1 / this.fixture.Bounds.Y, 1f)
			                * Matrix4.CreateTranslation(-0.5f, -0.5f, 0f)
			                * Matrix4.CreateScale(1.75f, 1.75f, 0f);
			Shader.Projection(ref this.projection);

			const BufferTarget target = BufferTarget.ArrayBuffer;
			this.vao = gl.GenVertexArray();
			gl.BindVertexArray(this.vao);
			{
				int i = 0;
				var leds = this.fixture.leds;
				this.vbo_leds = gl.GenBuffer();
				gl.BindBuffer(target, this.vbo_leds);
				gl.BufferData(target, leds.ByteSize(), leds, BufferUsageHint.StreamDraw);
				gl.VertexAttribFormat(i, 4, VertexAttribType.UnsignedByte, true, 0);
				gl.VertexAttribBinding(i, i);
				gl.EnableVertexAttribArray(i);
				gl.BindVertexBuffer(i, this.vbo_leds, 0, sizeof(RGBA));

				i = 1;
				var coords = this.fixture.coords;
				this.vbo_coords = gl.GenBuffer();
				gl.BindBuffer(target, this.vbo_coords);
				gl.BufferData(target, coords.ByteSize(), coords, BufferUsageHint.DynamicDraw);
				gl.VertexAttribFormat(i, 2, VertexAttribType.Float, false, 0);
				gl.VertexAttribBinding(i, i);
				gl.EnableVertexAttribArray(i);
				gl.BindVertexBuffer(i, this.vbo_coords, 0, sizeof(Vector2));

				gl.BindBuffer(target, 0);
			}
			gl.BindVertexArray(0);
		}

		void UnloadVAO() {
			gl.DeleteBuffer(this.vbo_leds);
			gl.DeleteBuffer(this.vbo_coords);
			gl.DeleteVertexArray(this.vao);
			this.vbo_leds = 0;
			this.vbo_coords = 0;
			this.vao = 0;
		}

		public void Resize(int newLength) {
			this.UnloadVAO();
			this.length = newLength;
			this.InitShader();
		}

		public void UpdateCoordsBuffer() {
			var coords = this.fixture.coords;
			gl.NamedBufferSubData(this.vbo_coords, 0, coords.ByteSize(), coords);
		}

		public void UpdateTexture() {
			var leds = this.fixture.leds;
			gl.NamedBufferSubData(this.vbo_leds, 0, leds.ByteSize(), leds);

			using (this.rt.Use())
			using (Shader.Use()) {
				glUtil.Clear();

				gl.BindVertexArray(this.vao);
				{
					gl.PointSize(this.fixture.template.ledSize / this.fixture.Bounds.X * Width * 0.875f);
					gl.DrawArrays(PrimitiveType.Points, 0, this.length);
				}
				gl.BindVertexArray(0);
			}
		}
	}

#endregion
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
	public static readonly AutoResetEvent sendFrameEvent = new(false);

	public static void Start() {
		httpListener.Prefixes.Add(Address);
		httpListener.Start();

		acceptClientsTask = Task.Run(LoopAcceptClients);
		sendTask = Task.Run(LoopSend);
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

	// const int SendBufferSize = Greg.BufferWidth * Greg.BufferWidth * 3 + 1;
	// static readonly byte[] sendBuffer = new byte[SendBufferSize];

	static async Task LoopSend() {
		// while (httpListener.IsListening) {
		// 	sendFrameEvent.WaitOne();
		//
		// 	// RGB[,] inputs = Greg.outputBuffer;
		// 	sendBuffer[0] = (byte)MessageType.SetLEDs;
		//
		// 	for (var y = 0; y < Greg.BufferWidth; y++) {
		// 		for (var x = 0; x < Greg.BufferWidth; x++) {
		// 			// var color = inputs[y, x];
		// 			RGBA color = new RGBA(0, 0, 0);
		//
		// 			var i = (y * Greg.BufferWidth + x) * 3 + 1;
		// 			sendBuffer[i + 0] = color.r;
		// 			sendBuffer[i + 1] = color.g;
		// 			sendBuffer[i + 2] = color.b;
		// 		}
		// 	}
		//
		// 	foreach ((string fixtureId, WebSocket client) in clients) {
		// 		if (client.State == WebSocketState.Open) {
		// 			try {
		// 				await client.SendAsync(sendBuffer, WebSocketMessageType.Binary, true, CancellationToken.None);
		// 			} catch (Exception ex) {
		// 				Console.WriteLine(ex);
		// 			}
		// 		}
		// 	}
		// }
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
