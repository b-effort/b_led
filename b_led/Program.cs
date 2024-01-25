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
- sequences
	- create/delete
	- time division
	- macro keyframes
- clip banks
	- clip dragging
	- rename
	- switch banks
	- map to push
- metronome pause
- ! led mapping
- sound reactive
	- fft
- ui
	- remember selected tabs
	- window opening/closing
- housekeeping
	- group ui into files by category (it's finally time)
	- gather config constants

# Reminders
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

var style = ImGui.GetStyle();
style.ItemInnerSpacing = vec2(8, 4);
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

var previewWindow = new PreviewWindow();
var palettesWindow = new PalettesWindow();
var patternsWindow = new PatternsWindow();
var sequencesWindow = new SequencesWindow();
var clipsWindow = new ClipsWindow();
var fixturesWindow = new FixturesWindow();
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
// FixtureServer.Start();

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
Shaders.Unload();
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
	fixturesWindow.Show();
	audioWindow.Show();
	metronomeWindow.Show();
	macrosWindow.Show();
	// funcPlotterWindow.Show();
	pushWindow.Show();
}

static class Config {
	public const string AssetsPath = "assets";

	public static readonly Vector2 FullPreviewResolution = vec2(800, 600);
	public static readonly Vector2 PatternPreviewResolution = vec2(64);
}

static class ImFonts {
	const string JetBrainsMono_Regular_TTF = $"{Config.AssetsPath}/JetBrainsMono-Regular.ttf";

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

static class Shaders {
	const string ShadersPath = $"{Config.AssetsPath}/shaders";

	public static readonly Shader FixturePreview;

	static Shaders() {
		FixturePreview = rl.LoadShader($"{ShadersPath}/fixture_preview.vert", $"{ShadersPath}/fixture_preview.frag");
	}

	public static void Unload() {
		rl.UnloadShader(FixturePreview);
	}
}
