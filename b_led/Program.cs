global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Numerics;
global using Color = b_effort.b_led.Color;
global using Raylib_cs;
global using rl = Raylib_cs.Raylib;
global using rlColor = Raylib_cs.Color;
global using rlImGui_cs;
global using ImGuiNET;
global using static b_effort.b_led.Color;
global using static b_effort.b_led.MethodImplShorthand;
global using static b_effort.b_led.VectorShorthand;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using b_effort.b_led;
using vtortola.WebSockets;
using vtortola.WebSockets.Rfc6455;
/*

# Runway
- matrix panel
- clip launching
- project files


# Mapping
https://electromage.com/docs/intro-to-mapping
1u = 1cm
([0, 1]], [0, 1])

buffers are [y, x] = [y * width + x]
 */

const int FPS = 60;
const int Width = 1280;
const int Height = 1280;

#region init

rl.SetConfigFlags(ConfigFlags.FLAG_WINDOW_RESIZABLE);
rl.InitWindow(Width, Height, "b_led");
rl.SetTargetFPS(FPS);

rlImGui.SetupUserFonts = ImFonts.SetupUserFonts;
rlImGui.Setup(enableDocking: true);
ImGui.GetStyle().ScaleAllSizes(1.30f);

ImGui.SetColorEditOptions(
	ImGuiColorEditFlags.NoAlpha
  | ImGuiColorEditFlags.Float
  | ImGuiColorEditFlags.InputHSV | ImGuiColorEditFlags.DisplayHSV
);

#endregion

#region app setup

using var previewWindow = new PreviewWindow();
using var palettesWindow = new PalettesWindow();
var patternsWindow = new PatternsWindow();
var clipsWindow = new ClipsWindow();
var metronomeWindow = new MetronomeWindow();
var macrosWindow = new MacrosWindow();
var pushWindow = new PushWindow();

var funcPlotterWindow = new FuncPlotterWindow {
	Funcs = new() {
		("saw", PatternScript.saw),
		("sine", PatternScript.sine),
		("triangle", PatternScript.triangle),
		("square", PatternScript.square),
	},
};
Push2.Connect();

FixtureServer.Start();

#endregion

while (!rl.WindowShouldClose()) {
	Update();

	rl.BeginDrawing();
	{
		rl.ClearBackground(rlColor.BLACK);

		rlImGui.Begin(Metronome.TDelta);
		DrawUI();
		rlImGui.End();
		rl.DrawFPS(Width - 84, 4);
	}
	rl.EndDrawing();
}

Push2.Dispose();
rlImGui.Shutdown();
rl.CloseWindow();
return;

void Update() {
	Push2.Update();
	Metronome.Tick();
	State.Update();

	FixtureServer.SendLEDs().Wait();
}

void DrawUI() {
	ImGui.DockSpaceOverViewport();
	previewWindow.Show();
	palettesWindow.Show();
	clipsWindow.Show();
	patternsWindow.Show();
	metronomeWindow.Show();
	macrosWindow.Show();
	funcPlotterWindow.Show();
	pushWindow.Show();
}

static class ImFonts {
	const string JetBrainsMono_Regular_TTF = "assets/JetBrainsMono-Regular.ttf";

	public static ImFontPtr Default { get; private set; }

	public static ImFontPtr Mono_15 { get; private set; }
	public static ImFontPtr Mono_17 { get; private set; }

	public static unsafe void SetupUserFonts(ImGuiIOPtr io) {
		io.Fonts.Clear();

		ImFontConfig fontCfg = new ImFontConfig {
			OversampleH = 3,
			OversampleV = 3,
			PixelSnapH = 1,
			RasterizerMultiply = 1,
			GlyphMaxAdvanceX = float.MaxValue,
			FontDataOwnedByAtlas = 1,
		};

		Mono_17 = io.LoadTTF(JetBrainsMono_Regular_TTF, 17, &fontCfg);
		rlImGui.LoadFontAwesome(px_to_pt(13));
		Mono_15 = io.LoadTTF(JetBrainsMono_Regular_TTF, 15, &fontCfg);

		Default = Mono_17;
	}

	static ImFontPtr LoadTTF(this ImGuiIOPtr io, string file, int px, ImFontConfigPtr config)
		=> io.Fonts.AddFontFromFileTTF(file, px_to_pt(px), config);

	static int px_to_pt(int px) => px * 96 / 72;
}

// struct LEDMap {
// 	public required string name;
// 	public required Vector2[] leds;
// }

// delegate LEDMap LEDMapper(int numPixels);

static class DragDropType {
	public const string Pattern = "pattern";
	public const string Palette = "palette";
}

static class State {
	public const int BufferWidth = 64;

	public static Pattern[] Patterns { get; }
	public static Pattern? CurrentPattern { get; set; }
	
	public static List<Palette> Palettes { get; }
	public static Palette? CurrentPalette { get; set; }
	
	public static ClipBank[] ClipBanks { get; }
	public static ClipBank CurrentClipBank { get; set; }

	static State() {
		Patterns = new Pattern[] {
			new TestPattern(),
			new HSBDemoPattern(),
			new EdgeBurstPattern(),
		};
		foreach (var pattern in Patterns) {
			pattern.Update();
		}
		CurrentPattern = Patterns[0];

		Palettes = new List<Palette> {
			new("b&w"),
			new(
				"rainbow", new Gradient(
					new Gradient.Point[] {
						new(0f, hsb(0f)),
						new(1f, hsb(1f)),
					}
				)
			),
			new(
				"cyan-magenta", new Gradient(
					new Gradient.Point[] {
						new(0f, hsb(170 / 360f)),
						new(1f, hsb(320 / 360f)),
					}
				)
			),
		};
		CurrentPalette = Palettes[2];

		ClipBanks = new ClipBank[] {
			new("bank 1"),
			new("bank 2"),
			new("bank 3"),
			new("bank 4"),
			new("bank 5"),
			new("bank 6"),
			new("bank 7"),
			new("bank 8"),
		};
		CurrentClipBank = ClipBanks[0];
	}
	
	// public static LEDMapper[] LEDMappers { get; set; } = Array.Empty<LEDMapper>();

	public static readonly HSB[,] previewBuffer = new HSB[BufferWidth, BufferWidth];

	public static void Update() {
		if (CurrentPattern is null)
			return;

		CurrentPattern.Update();

		HSB[,] outputs = previewBuffer;
		HSB[,] patternPixels = CurrentPattern.pixels;
		float hueOffset = Macro.hue_offset.Value;
		var gradient = CurrentPalette?.Gradient;

		for (var y = 0; y < BufferWidth; y++) {
			for (var x = 0; x < BufferWidth; x++) {
				var color = patternPixels[y, x];
				if (gradient != null) {
					color = gradient.MapColor(
						color with {
							h = MathF.Abs(color.h + hueOffset) % 1f,
						}
					);
				}
				outputs[y, x] = color;
			}
		}
	}
}

enum ClipType {
	Pattern,
	Palette,
}

sealed class Clip {
	public ClipType Type { get; private set; }
	public bool IsTriggered { get; set; }

	Pattern? pattern;
	public Pattern? Pattern {
		get => this.pattern;
		set {
			this.pattern = value;
			if (value != null) {
				this.Type = ClipType.Pattern;
				this.palette = null;
			}
		}
	}

	Palette? palette;
	public Palette? Palette {
		get => this.palette;
		set {
			this.palette = value;
			if (value != null) {
				this.Type = ClipType.Palette;
				this.pattern = null;
			}
		}
	}

	public bool HasContents => this.Type switch {
		ClipType.Pattern => this.pattern != null, 
		ClipType.Palette => this.palette != null,
		_                => throw new ArgumentOutOfRangeException()
	};

	public bool IsActive => this.IsTriggered && this.HasContents;
}

sealed class ClipBank {
	public readonly int numCols = 8;
	public readonly int numRows = 8;
	
	public string name;
	public readonly Clip[,] clips;

	public ClipBank(string name) {
		this.name = name;
		
		this.clips = new Clip[this.numRows, this.numCols];
		for (var y = 0; y < this.numRows; y++) {
			for (var x = 0; x < this.numCols; x++) {
				this.clips[y, x] = new Clip();
			}
		}
	}
}

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
				WebSocket? client = await ws.AcceptWebSocketAsync(CancellationToken.None).ConfigureAwait(false);
				if (client?.IsConnected != true)
					continue;
				Console.WriteLine($"Client connected: {client.RemoteEndpoint.Serialize()}");
					
				await client.WriteBytesAsync(new[] { (byte)MessageType.GetId }).ConfigureAwait(false);
				Console.WriteLine("Requested fixture id");
				
				string? fixtureId = await client.ReadStringAsync(CancellationToken.None).ConfigureAwait(false);
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
	
	public static async Task SendLEDs() {
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

		foreach ((string fixtureId, WebSocket client) in clients) {
			if (client.IsConnected) {
				try {
					// Console.WriteLine($"Sending leds to {fixtureId}");
					await client.WriteBytesAsync(ledBuffer);
					// await client.ReadMessageAsync(CancellationToken.None);
				} catch (Exception ex) {
					Console.WriteLine(ex);
				}
			}
		}
	}
}
