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
global using fftw = SharpFFTW.Single;
global using static b_effort.b_led.Color;
using System.Diagnostics;
using System.Threading;
using b_effort.b_led;

/*

# Runway
- ! sequences
	- create/delete
	- ! time division
	- macro keyframes
- clip banks
	- clip dragging
	- rename
	- switch banks
	- map to push
- metronome pause
- led mapping
- sound reactive
	- fft
- gather config constants


# Mapping
https://electromage.com/docs/intro-to-mapping
1u = 1cm
([0, 1]], [0, 1])

buffers are [y, x] = [y * width + x]
 */

const int FPS = 144;
int width = 1280;
int height = 1280;

#region init

if (OperatingSystem.IsWindows()) {
	Thread.CurrentThread.SetApartmentState(ApartmentState.Unknown);
	Thread.CurrentThread.SetApartmentState(ApartmentState.STA);
}

rl.SetConfigFlags(ConfigFlags.FLAG_WINDOW_RESIZABLE);
rl.InitWindow(width, height, "b_led");
rl.SetTargetFPS(FPS);

rlImGui.SetupUserFonts = ImFonts.SetupUserFonts;
rlImGui.Setup(enableDocking: true);
// ImGui.GetStyle().ScaleAllSizes(1.30f);

ImGui.SetColorEditOptions(
	ImGuiColorEditFlags.NoAlpha
  | ImGuiColorEditFlags.Float
  | ImGuiColorEditFlags.InputHSV | ImGuiColorEditFlags.DisplayHSV
);

#endregion

#region app setup

try {
	Greg.LoadProject();
} catch (Exception e) {
	Console.WriteLine(e);
	Greg.LoadDemoProject();
}

using var previewWindow = new PreviewWindow();
var palettesWindow = new PalettesWindow();
var patternsWindow = new PatternsWindow();
var sequencesWindow = new SequencesWindow();
var clipsWindow = new ClipsWindow();
var audioWindow = new AudioWindow();
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

var deltaTimer = Stopwatch.StartNew();
float deltaTime = 1f / FPS;

while (!rl.WindowShouldClose()) {
	if (rl.IsWindowResized()) {
		width = rl.GetRenderWidth();
		height = rl.GetRenderHeight();
	}
	
	Update(deltaTime);

	rl.BeginDrawing();
	{
		rl.ClearBackground(rlColor.BLACK);

		rlImGui.Begin(deltaTime);
		DrawUI();
		rlImGui.End();
		rl.DrawFPS(width - 84, 4);
	}
	rl.EndDrawing();
	
	deltaTime = (float)deltaTimer.Elapsed.TotalSeconds;
	deltaTimer.Restart();
}

AudioIn.Close();
Push2.Dispose();
rlImGui.Shutdown();
rl.CloseWindow();
return;

void Update(float deltaTime) {
	Push2.Update();
	Metronome.Tickle(deltaTime);
	Greg.Update();

	FixtureServer.Update(deltaTime);
}

void DrawUI() {
	ImGui.DockSpaceOverViewport();
	
	if (ImGui.BeginMainMenuBar()) {
		if (ImGui.BeginMenu("File")) {
			if (ImGui.MenuItem("Save")) {
				Greg.SaveProject();
			}
			if (ImGui.MenuItem("Load")) {
				Greg.LoadProject();
			}
			if (ImGui.MenuItem("Load demo project")) {
				Greg.LoadDemoProject();
			}
		}
	}
	
	previewWindow.Show();
	palettesWindow.Show();
	clipsWindow.Show();
	sequencesWindow.Show();
	patternsWindow.Show();
	audioWindow.Show();
	metronomeWindow.Show();
	macrosWindow.Show();
	// funcPlotterWindow.Show();
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

// greg is secretary of state
// greg is a beast you can't tame
static class Greg {
	const string ProjectFile = $"test.{Project.FileExt}";

	public const int BufferWidth = 64;

	static Project project = new();

	public static List<Palette> Palettes => project.Palettes;
	public static Pattern[] Patterns => Pattern.All;
	public static List<Sequence> Sequences => project.Sequences;
	public static ClipBank[] ClipBanks => project.ClipBanks;
	
	public static ClipBank? ActiveClipBank { get; set; } = ClipBanks[0];
	public static Palette? ActivePalette => ActiveClipBank?.ActivePalette;
	public static Pattern? ActivePattern => ActiveClipBank?.ActivePattern;


	public static void LoadDemoProject() {
		project = new Project {
			Palettes = {
				new("b&w"),
				new("rainbow", new(
					new Gradient.Point[] {
						new(0f, hsb(0f)),
						new(1f, hsb(1f)),
					}
				)),
				new("cyan-magenta", new(
					new Gradient.Point[] {
						new(0f, hsb(170 / 360f)),
						new(1f, hsb(320 / 360f)),
					}
				)),
			},
		};
		ActiveClipBank = ClipBanks[0];
	}

	static Greg() {
		// init preview
		foreach (var pattern in Patterns) {
			pattern.Update();
		}
	}

	public static void SaveProject() {
		project.Save(ProjectFile);
	}

	public static void LoadProject() {
		project = Project.Load(ProjectFile);
		ActiveClipBank = ClipBanks[0];
	}
	
	// public static LEDMapper[] LEDMappers { get; set; } = Array.Empty<LEDMapper>();

	public static readonly RGB[,] outputBuffer = new RGB[BufferWidth, BufferWidth];

	public static void Update() {
		var pattern = ActivePattern;
		if (pattern == null)
			return;

		pattern.Update();

		RGB[,] outputs = outputBuffer;
		HSB[,] patternPixels = pattern.pixels;
		float hueOffset = Macro.hue_offset.Value;
		var gradient = ActivePalette?.gradient;

		for (var y = 0; y < BufferWidth; y++)
		for (var x = 0; x < BufferWidth; x++) {
			HSB color = patternPixels[y, x];
			
			color.h += hueOffset;
			if (color.h > 1f)
				color.h %= 1f;
			if (gradient != null)
				color = gradient.MapColor(color);
			
			outputs[y, x] = color.ToRGB();
		}
	}
}
