global using System;
global using System.Numerics;
global using rlImGui_cs;
global using ImGuiNET;
global using rl = Raylib_cs.Raylib;
global using rlColor = Raylib_cs.Color;
using System.Diagnostics;
using b_effort.b_led;
using Raylib_cs;

//# static

/*
# Mapping
https://electromage.com/docs/intro-to-mapping
1u = 1cm
([0, 1]], [0, 1])

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

rl.SetConfigFlags(ConfigFlags.FLAG_WINDOW_RESIZABLE);
rl.InitWindow(Width, Height, "b_led");
rl.SetTargetFPS(FPS);

rlImGui.Setup(enableDocking: true);
unsafe {
	ImGui.GetIO().NativePtr->IniFilename = null;
}

var state = new State() {
	Pattern = new SinPattern(),
	ColorPattern = new HSBDemoPattern(),
};

using var previewWindow = new PreviewWindow(state);

var deltaTimer = Stopwatch.StartNew();
float deltaTime = 1f / FPS;

while (!rl.WindowShouldClose()) {
	Update();

	rl.BeginDrawing();
	{
		rl.ClearBackground(rlColor.BLACK);
		rl.DrawFPS(8, 8);

		rlImGui.Begin(deltaTime);
		DrawUI();
		rlImGui.End();
	}
	rl.EndDrawing();

	deltaTime = (float)deltaTimer.Elapsed.TotalSeconds;
	deltaTimer.Restart();
}

rlImGui.Shutdown();
rl.CloseWindow();
return;

void Update() {
	state.Update();
	previewWindow.Update();
}

void DrawUI() {
	previewWindow.Show();
}

sealed class State {
	public const int BufferSize = 128;

	public required Pattern Pattern { get; set; }
	public required ColorPattern ColorPattern { get; set; }

	public readonly Color.RGB[,] outputBuffer = new Color.RGB[BufferSize, BufferSize];

	public void Update() {
		var outputs = this.outputBuffer;

		for (var x = 0; x < BufferSize; x++) {
			for (var y = 0; y < BufferSize; y++) {
				int i = x + y * BufferSize;
				float xPercent = x / (float)(BufferSize - 1);
				float yPercent = y / (float)(BufferSize - 1);

				var brightness = this.Pattern.Generate(i, xPercent, yPercent);
				var hs = this.ColorPattern.Generate(i, xPercent, yPercent);
				outputs[x, y] = new Color.HSB(hs, brightness).ToRGB();
			}
		}
	}
}

#region windows

sealed class PreviewWindow : IDisposable {
	const int Resolution = State.BufferSize;

	readonly Image image;
	readonly Texture2D texture;

	readonly State state;

	public PreviewWindow(State state) {
		this.state = state;
		this.image = rl.GenImageColor(Resolution, Resolution, rlColor.BLACK);
		this.texture = rl.LoadTextureFromImage(this.image);
	}

	~PreviewWindow() {
		this.Dispose();
	}

	public void Dispose() {
		rl.UnloadImage(this.image);
		rl.UnloadTexture(this.texture);
		GC.SuppressFinalize(this);
	}

	public unsafe void Update() {
		var pixels = (rlColor*)this.image.data;
		var buffer = this.state.outputBuffer;
		for (var x = 0; x < Resolution; x++) {
			for (var y = 0; y < Resolution; y++) {
				pixels[x + y * Resolution] = buffer[x, y];
			}
		}

		rl.UpdateTexture(this.texture, pixels);
	}

	public void Show() {
		ImGui.SetNextWindowSize(new Vector2(512));
		ImGui.Begin("preview");
		{
			imUtil.ImageTextureFit(this.texture);
		}
		ImGui.End();
	}
}

#endregion

#region framework stuff

interface Pattern {
	Color.B Generate(int index, float x, float y);
}


interface ColorPattern {
	Color.HS Generate(int index, float x, float y);
}

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

static class BMath {
	public const float PI2 = MathF.PI * 2;

	public static float Sin01(float x) {
		float sin = MathF.Sin(x * PI2);
		return (sin + 1) / 2;
	}

	public static float Cos01(float x) {
		float cos = MathF.Cos(x * PI2);
		return (cos + 1) / 2;
	}
}

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
