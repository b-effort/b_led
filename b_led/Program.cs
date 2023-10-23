#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
//# global
global using System;
global using System.Numerics;
global using rlImGui_cs;
global using ImGuiNET;
//# aliases
global using rl = Raylib_cs.Raylib;
global using rlColor = Raylib_cs.Color;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
	Generator = new SinGenerator(),
	ColorGenerator = new HSBDemoColorGenerator(),
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

	public required Generator Generator { get; set; }
	public required ColorGenerator ColorGenerator { get; set; }

	public readonly Color.RGB[,] outputBuffer = new Color.RGB[BufferSize, BufferSize];

	public void Update() {
		var outputs = this.outputBuffer;

		for (var x = 0; x < BufferSize; x++) {
			for (var y = 0; y < BufferSize; y++) {
				int i = x + y * BufferSize;
				float xPercent = x / (float)(BufferSize - 1);
				float yPercent = y / (float)(BufferSize - 1);

				var brightness = this.Generator.Generate(i, xPercent, yPercent);
				var hs = this.ColorGenerator.Generate(i, xPercent, yPercent);
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

interface Generator {
	float Generate(int index, float x, float y);
}

class GeneratorSlot {
	public Generator Generator { get; }
	public IntensityBlendMode BlendMode { get; }

	public GeneratorSlot(Generator generator, IntensityBlendMode blendMode) {
		this.Generator = generator;
		this.BlendMode = blendMode;
	}
}

interface ColorGenerator {
	Color.HS Generate(int index, float x, float y);
}

class GeneratorGroup : Generator {
	readonly GeneratorSlot[] generators;

	public GeneratorGroup(params GeneratorSlot[] generators) {
		this.generators = generators;
	}

	public float Generate(int index, float x, float y) {
		float result = float.NaN;

		foreach (var slot in this.generators) {
			float currentValue = slot.Generator.Generate(index, x, y);

			result = float.IsNaN(result)
				? currentValue
				: slot.BlendMode.Blend(result, currentValue);
		}

		return result;
	}
}

enum IntensityBlendMode {
	LAST,
	AND,
	OR,
	NOT,
	XOR,
	ADD,
	SUB,
	AVG,
}

static class IntensityBlendModeExtensions {
	public static float Blend(this IntensityBlendMode mode, float a, float b, float threshold = 0.01f) {
		return mode switch {
			IntensityBlendMode.LAST => b,
			IntensityBlendMode.AND  => b >= threshold ? a : 0,
			IntensityBlendMode.OR   => MathF.Max(a, b),
			IntensityBlendMode.NOT  => b >= threshold ? 0 : a,
			IntensityBlendMode.XOR  => (a >= threshold) ^ (b >= threshold) ? MathF.Max(a, b) : 0,
			IntensityBlendMode.ADD  => a + b,
			IntensityBlendMode.SUB  => a - b,
			IntensityBlendMode.AVG  => (a + b) / 2,
			_                       => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
		};
	}
}

#endregion

static class Clock {
	static readonly Stopwatch timer = Stopwatch.StartNew();

	public static float T => (float)timer.Elapsed.TotalSeconds;
}

#region generators

sealed class HSBDemoGenerator : Generator {
	public float Generate(int index, float x, float y) {
		return x >= 0.5 && y < 0.5
			? (MathF.Sin((y - 0.25f) * 2 * MathF.PI) + 1) / 2
			: 1;
	}
}

sealed class SinGenerator : Generator {
	public float Generate(int index, float x, float y) {
		return MathF.Sin(x + Clock.T);
	}
}

#endregion

#region color generators

sealed class HSBDemoColorGenerator : ColorGenerator {
	public Color.HS Generate(int index, float x, float y) {
		return new Color.HS(
			x,
			x < 0.5 && y < 0.5
				? y * 2
				: 1
		);
	}
}

#endregion

static class imUtil {
	[SuppressMessage("ReSharper", "PossibleLossOfFraction")]
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
			ImGui.SetCursorPosX(area.X / 2 - sizeX / 2);
			ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (area.Y / 2 - sizeY / 2));
		}

		rlImGui.ImageSize(texture, sizeX, sizeY);
	}
}
