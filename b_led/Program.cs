global using System;
global using System.Collections.Generic;
global using System.Numerics;
global using rlImGui_cs;
global using ImGuiNET;
global using rl = Raylib_cs.Raylib;
global using rlColor = Raylib_cs.Color;
global using MethodImplAttribute = System.Runtime.CompilerServices.MethodImplAttribute;
global using MethodImplOptions = System.Runtime.CompilerServices.MethodImplOptions;
using System.Diagnostics;
using b_effort.b_led;
using Raylib_cs;


/*

# Runway
- clock ui


# Mapping
https://electromage.com/docs/intro-to-mapping
1u = 1cm
([0, 1]], [0, 1])

buffers are [y, x]

# FX
Can be combined with boolean (& | ! ^) or f(a, b) logic.
One class can be multiple types.

Types:
	- brightness
	- color (hue & saturation)
 */

const int FPS = 144;
const int Width = 1920;
const int Height = 1200;

#region setup

rl.SetConfigFlags(ConfigFlags.FLAG_WINDOW_RESIZABLE);
rl.InitWindow(Width, Height, "b_led");
rl.SetTargetFPS(FPS);

rlImGui.SetupUserFonts = io => {
	io.Fonts.Clear();
	ImFontConfig fontCfg = new ImFontConfig {
		OversampleH = 3,
		OversampleV = 3,
		PixelSnapH = 1,
		RasterizerMultiply = 1,
		GlyphMaxAdvanceX = float.MaxValue,
		FontDataOwnedByAtlas = 1,
	};
	unsafe { //
		const int sizePixels = 17 * 96 / 72;
		io.Fonts.AddFontFromFileTTF("assets/JetBrainsMono-Regular.ttf", sizePixels, &fontCfg);
	}
};
rlImGui.Setup(enableDocking: true);
ImGui.GetStyle().ScaleAllSizes(22 / 13f);

var state = new State() {
	Pattern = new TestPattern(),
	LEDMappers = Array.Empty<LEDMapper>(),
};

using var previewWindow = new PreviewWindow(state);
var metronomeWindow = new MetronomeWindow();
var funcPlotterWindow = new FuncPlotterWindow {
	Funcs = new() {
		("saw", PatternScript.saw),
		("sine", PatternScript.sine),
		("triangle", PatternScript.triangle),
		("square", PatternScript.square),
	},
};

#endregion

var deltaTimer = Stopwatch.StartNew();
float deltaTime = 1f / FPS;

while (!rl.WindowShouldClose()) {
	Update(deltaTime);

	rl.BeginDrawing();
	{
		rl.ClearBackground(rlColor.BLACK);

		rlImGui.Begin(deltaTime);
		DrawUI();
		rlImGui.End();
		rl.DrawFPS(Width - 88, 8);
	}
	rl.EndDrawing();

	deltaTime = (float)deltaTimer.Elapsed.TotalSeconds;
	deltaTimer.Restart();
}

rlImGui.Shutdown();
rl.CloseWindow();
return;

void Update(float dt) {
	Metronome.Tick();
	state.Update(dt);
}

void DrawUI() {
	ImGui.DockSpaceOverViewport();
	previewWindow.Show();
	metronomeWindow.Show();
	funcPlotterWindow.Show();
	// LEDMap x = new LEDMap() {
	// 	name = "",
	// 	leds = Array.Empty<Vector2>(),
	// };
}

struct LEDMap {
	public required string name;
	public required Vector2[] leds;
}

delegate LEDMap LEDMapper(int numPixels);

sealed class State {
	public const int BufferWidth = 128;
	public const int NumPixels = 128;

	public required Pattern Pattern { get; set; }
	public required LEDMapper[] LEDMappers { get; set; }

	public readonly Color.RGB[,] previewBuffer = new Color.RGB[BufferWidth, BufferWidth];

	public void Update(float dt) {
		this.Pattern.Update(dt);

		var outputs = this.previewBuffer;
		var patternPixels = this.Pattern.pixels;

		for (var y = 0; y < BufferWidth; y++) {
			for (var x = 0; x < BufferWidth; x++) {
				outputs[y, x] = patternPixels[y, x].ToRGB();
			}
		}
	}
}
