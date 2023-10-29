global using System;
global using System.Collections.Generic;
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
using b_effort.b_led;

/*

# Runway
- pattern banks
- palettes


# Mapping
https://electromage.com/docs/intro-to-mapping
1u = 1cm
([0, 1]], [0, 1])

buffers are [y, x] = [y * width + x]
 */

const int FPS = 144;
const int Width = 1280;
const int Height = 720;

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
ImGui.GetStyle().ScaleAllSizes(1.30f);

var state = new State {
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
var pushWindow = new PushWindow();

#endregion

while (!rl.WindowShouldClose()) {
	Update();

	rl.BeginDrawing();
	{
		rl.ClearBackground(rlColor.BLACK);

		rlImGui.Begin(Metronome.TDelta);
		DrawUI();
		rlImGui.End();
		rl.DrawFPS(Width - 88, 8);
	}
	rl.EndDrawing();
}

Push2.Dispose();
rlImGui.Shutdown();
rl.CloseWindow();
return;

void Update() {
	Metronome.Tick();
	state.Update();
}

void DrawUI() {
	ImGui.DockSpaceOverViewport();
	previewWindow.Show();
	metronomeWindow.Show();
	funcPlotterWindow.Show();
	pushWindow.Show();
}

struct LEDMap {
	public required string name;
	public required Vector2[] leds;
}

delegate LEDMap LEDMapper(int numPixels);

sealed class State {
	public const int BufferWidth = 256;
	public const int NumPixels = 128;

	public required Pattern Pattern { get; set; }
	public required LEDMapper[] LEDMappers { get; set; }

	public readonly RGB[,] previewBuffer = new RGB[BufferWidth, BufferWidth];

	public void Update() {
		this.Pattern.Update();

		var outputs = this.previewBuffer;
		var patternPixels = this.Pattern.pixels;

		for (var y = 0; y < BufferWidth; y++) {
			for (var x = 0; x < BufferWidth; x++) {
				outputs[y, x] = patternPixels[y, x].ToRGB();
			}
		}
	}
}
