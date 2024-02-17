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
	public RGBA[] Leds => this.leds;
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
	}

	public Fixture(string name = "") : this(
		id: Guid.NewGuid(),
		name,
		template_id: FixtureTemplate.Default.Id
	) { }

	public FixtureSocket? Socket() => Greg.FixtureSockets.Find(sock => sock.hostname == this.hostname);

	public void Render(Pattern pattern, Palette? palette) =>
		pattern.RenderTo(this.leds, this.coords, this.Bounds, palette);

	public void Resize() {
		Array.Resize(ref this.leds, this.numLeds);
		Array.Resize(ref this.coords, this.numLeds);
		this.RebuildMap();
	 	this.preview.Resize(this.numLeds);
	}

	void RebuildMap() {
		this.Template.PopulateMap(this.coords);
		this.Bounds = GetBounds(this.coords);
	}

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

sealed class FixtureSocket {
	enum MessageType : byte {
		SetLEDs = 0,
	}

	public enum ConnectionState
	{
		Disconnected,
		Connected,
		Connecting,
		Reconnecting,
	}

	public readonly string hostname;
	readonly Uri uri;
	ClientWebSocket? ws;
	Timer? reconnect;
	public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
	Fixture[] fixtures = Array.Empty<Fixture>();

	Task? send_task;
	CancellationTokenSource? send_cts;
	byte[] send_buf = Array.Empty<byte>();
	readonly AutoResetEvent send_event = new(false);

	public bool pause = false;

	public FixtureSocket(string hostname) {
		this.hostname = hostname;
		this.uri = new Uri($"ws://{hostname}.local:{Config.WS_Port}/{Config.WS_Path}/");
	}

	public void AssignFixtures(Fixture[] newFixtures) {
		this.fixtures = newFixtures;
		int bufSize = this.fixtures.Sum(f => f.numLeds) * 3 + 1;
		Array.Resize(ref this.send_buf, bufSize);
	}

	public void Connect() {
		if (this.State == ConnectionState.Connecting || this.State == ConnectionState.Connected)
			throw new OopsiePoopsie($"Fixture {this.hostname} already connected");

		this.send_cts = new CancellationTokenSource();
		this.send_task = Task.Run(() => this.LoopSend(this.send_cts.Token));

		this.reconnect = new Timer(
			_ => {
				if (this.State == ConnectionState.Disconnected) {
					this.State = ConnectionState.Reconnecting;
					this.Connect();
				}
			}, null, Config.WS_Reconnect_ms, Config.WS_Reconnect_ms);
	}

	public async void Disconnect() {
		if (this.State != ConnectionState.Connecting && this.State != ConnectionState.Connected)
			throw new OopsiePoopsie($"Fixture {this.hostname} isn't connected");

		this.send_cts!.Cancel();
		this.send_cts.Dispose();
		this.send_cts = null;

		await this.reconnect!.DisposeAsync();
		this.reconnect = null;

		await this.send_task!;
	}

	public void SignalSend() {
		this.send_event.Set();
	}

	async Task LoopSend(CancellationToken cancel) {
		// todo: make ws local
		this.ws = new ClientWebSocket();
		if (this.State != ConnectionState.Reconnecting) {
			this.State = ConnectionState.Connecting;
		}

		try {
			await this.ws.ConnectAsync(this.uri, CancellationToken.None);
			Console.WriteLine($"Fixture {this.hostname} connected");

			WaitHandle[] waitHandles = { this.send_event, cancel.WaitHandle };
			while (this.ws.State == WebSocketState.Open) {
				WaitHandle.WaitAny(waitHandles);
				if (cancel.IsCancellationRequested) {
					break;
				}
				if (this.pause) {
					continue;
				}

				var buf = this.send_buf;
				buf[0] = (byte)MessageType.SetLEDs;
				foreach (var fixture in this.fixtures) {
					var offset = fixture.startingLedOffset;
					var leds = fixture.Leds;

					for (var iLed = 0; iLed < leds.Length; iLed++) {
						RGBA led = leds[iLed];
						int i = 1 + offset + iLed;
						buf[i + 0] = led.r;
						buf[i + 1] = led.g;
						buf[i + 2] = led.b;
					}
				}

				try {
					await this.ws.SendAsync(buf, WebSocketMessageType.Binary, true, CancellationToken.None);
				} catch (Exception ex) {
					Console.WriteLine(ex);
					throw;
				}
			}

			if (this.ws.State == WebSocketState.Open) {
				await this.ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
			}

			Console.WriteLine($"Fixture {this.hostname} disconnected: {this.ws.State}, cancelled: {cancel.IsCancellationRequested}");
		} catch {
			// ignored
		}

		this.State = ConnectionState.Disconnected;
		this.ws.Dispose();
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
