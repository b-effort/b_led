global using System;
global using System.Numerics;
global using rlImGui_cs;
global using ImGuiNET;
global using rl = Raylib_cs.Raylib;
global using rlColor = Raylib_cs.Color;
using System.Collections.Generic;
using System.Diagnostics;
using b_effort.b_led;
using Raylib_cs;

//# static

/*
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
	state.Update(dt);
}

void DrawUI() {
	ImGui.DockSpaceOverViewport();
	previewWindow.Show();
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
	public const int BufferWidth = 64;
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

#region windows

sealed class PreviewWindow : IDisposable {
	const int Resolution = State.BufferWidth;

	readonly Image image;
	readonly Texture2D texture;

	readonly State state;

	public PreviewWindow(State state) {
		this.state = state;
		this.image = rl.GenImageColor(Resolution, Resolution, rlColor.BLACK);
		this.texture = rl.LoadTextureFromImage(this.image);
	}

	~PreviewWindow() => this.Dispose();

	public void Dispose() {
		rl.UnloadImage(this.image);
		rl.UnloadTexture(this.texture);
		GC.SuppressFinalize(this);
	}

	public unsafe void Show() {
		var pixels = (rlColor*)this.image.data;
		var buffer = this.state.previewBuffer;
		for (var y = 0; y < Resolution; y++) {
			for (var x = 0; x < Resolution; x++) {
				pixels[y * Resolution + x] = buffer[y, x];
			}
		}
		rl.UpdateTexture(this.texture, pixels);

		ImGui.SetNextWindowSize(new Vector2(512));
		ImGui.Begin("preview");
		{
			imUtil.ImageTextureFit(this.texture);
		}
		ImGui.End();
	}
}

sealed class FuncPlotterWindow {
	const int Resolution = State.BufferWidth;

	public List<(string name, Func<float, float> f)> Funcs { get; init; } = new();

	readonly float[] points = new float[Resolution];

	bool animate;

	public void Show() {
		ImGui.SetNextWindowSize(new Vector2(20, 10) * ImGui.GetFontSize(), ImGuiCond.FirstUseEver);
		ImGui.Begin("f(x) plotter");
		{
			ImGui.Checkbox("animate", ref this.animate);

			var points = this.points;
			foreach (var (name, f) in this.Funcs) {
				for (var i = 0; i < Resolution; i++) {
					var x = (float)i / Resolution;
					if (this.animate)
						x += PatternScript.t / 2;
					points[i] = f(x * 2);
				}
				ImGui.PlotLines(name, ref points[0], points.Length);
			}
		}
		ImGui.End();
	}
}

#endregion

static class imUtil {
	public static void ImageTextureFit(Texture2D texture, bool center = true) {
		Vector2 area = ImGui.GetContentRegionAvail();

		float scale = area.X / texture.width;
		float y = texture.height * scale;
		if (y > area.Y) {
			scale = area.Y / texture.height;
		}

		int sizeX = (int)(texture.width * scale);
		int sizeY = (int)(texture.height * scale);

		if (center) {
			ImGui.SetCursorPosX(0);
			// ReSharper disable PossibleLossOfFraction
			ImGui.SetCursorPosX(area.X / 2 - sizeX / 2);
			ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (area.Y / 2 - sizeY / 2));
			// ReSharper restore PossibleLossOfFraction
		}

		rlImGui.ImageSize(texture, sizeX, sizeY);
	}
}

#region stuff i shouldn't have written yet

enum BrightnessBlendMode {
	LAST,
	AND,
	OR,
	NOT,
	XOR,
	ADD,
	SUB,
	AVG,
}

static class BrightnessBlendModeExtensions {
	public static float Blend(this BrightnessBlendMode mode, float a, float b, float threshold = 0.01f) {
		return mode switch {
			BrightnessBlendMode.LAST => b,
			BrightnessBlendMode.AND  => b >= threshold ? a : 0,
			BrightnessBlendMode.OR   => MathF.Max(a, b),
			BrightnessBlendMode.NOT  => b >= threshold ? 0 : a,
			BrightnessBlendMode.XOR  => (a >= threshold) ^ (b >= threshold) ? MathF.Max(a, b) : 0,
			BrightnessBlendMode.ADD  => a + b,
			BrightnessBlendMode.SUB  => a - b,
			BrightnessBlendMode.AVG  => (a + b) / 2,
			_                        => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
		};
	}
}

#endregion
