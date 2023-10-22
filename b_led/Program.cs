#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
//# global
global using System;
global using System.Numerics;
global using rlImGui_cs;
global using ImGuiNET;
//# aliases
global using rl = Raylib_cs.Raylib;
//# static
global using static System.MathF;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Raylib_cs;
using rlColor = Raylib_cs.Color;

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
	Generator = new SineWaveGenerator(),
	ColorGenerator = new RainbowColorGenerator(),
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
	public const int Resolution = 256;

	public required Generator Generator { get; set; }
	public required ColorGenerator ColorGenerator { get; set; }

	public readonly float[,] brightnessBuffer = new float[Resolution, Resolution];
	public readonly ColorHS[,] hsBuffer = new ColorHS[Resolution, Resolution];
	public readonly ColorHSB[,] hsbBuffer = new ColorHSB[Resolution, Resolution];
	public readonly ColorRGB[,] outputBuffer = new ColorRGB[Resolution, Resolution];

	public void Update() {
		var brightnesses = this.brightnessBuffer;
		var colorsHs = this.hsBuffer;
		var colorsHsb = this.hsbBuffer;
		var outputs = this.outputBuffer;

		for (var x = 0; x < Resolution; x++) {
			for (var y = 0; y < Resolution; y++) {
				int i = x + y * Resolution;
				float xPercent = x / (float)(Resolution - 1);
				float yPercent = y / (float)(Resolution - 1);

				var brightness = brightnesses[x, y] = this.Generator.Generate(i, xPercent, yPercent);
				var hs = colorsHs[x, y] = this.ColorGenerator.Generate(i, xPercent, yPercent);
				var hsb = colorsHsb[x, y] = new ColorHSB(hs, brightness);
				outputs[x, y] = hsb.ToRGB();
			}
		}
	}
}

#region windows

sealed class PreviewWindow : IDisposable {
	readonly RenderTexture2D rt = rl.LoadRenderTexture(State.Resolution, State.Resolution);
	readonly State state;

	public PreviewWindow(State state) {
		this.state = state;
	}

	~PreviewWindow() {
		this.Dispose();
	}

	public void Dispose() {
		rl.UnloadRenderTexture(this.rt);
		GC.SuppressFinalize(this);
	}

	public void Update() {
		rl.BeginTextureMode(this.rt);
		{
			rl.ClearBackground(rlColor.BLACK);
			var outputBuffer = this.state.outputBuffer;
			var resolution = outputBuffer.GetLength(0);
			for (var x = 0; x < resolution; x++) {
				for (var y = 0; y < resolution; y++) {
					rl.DrawPixel(x, y, outputBuffer[x, y]);
				}
			}
		}
		rl.EndTextureMode();
	}

	public void Show() {
		ImGui.SetNextWindowSize(new Vector2(512));
		ImGui.Begin("preview");
		{
			// Vector2 size = ImGui.GetContentRegionAvail();
			rlImGui.ImageRenderTextureFit(this.rt);
		}
		ImGui.End();
	}
}

#endregion

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
	ColorHS Generate(int index, float x, float y);
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
			IntensityBlendMode.OR   => Max(a, b),
			IntensityBlendMode.NOT  => b >= threshold ? 0 : a,
			IntensityBlendMode.XOR  => (a >= threshold) ^ (b >= threshold) ? Max(a, b) : 0,
			IntensityBlendMode.ADD  => a + b,
			IntensityBlendMode.SUB  => a - b,
			IntensityBlendMode.AVG  => (a + b) / 2,
			_                       => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
		};
	}
}

#region color

record struct ColorRGB(int r, int g, int b) {
	public static implicit operator rlColor(ColorRGB @this) => new(@this.r, @this.g, @this.b, 255);
}

record struct ColorHS(float h, float s) { }

record struct ColorHSB(float h, float s, float b) {
	public ColorHSB(ColorHS hs, float b) : this(hs.h, hs.s, b) { }

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ColorRGB ToRGB() {
		if (this.s == 0) {
			var value = (int)(this.b * 255 + 0.5f);
			return new ColorRGB(value, value, value);
		}

		float chroma = this.b * this.s;
		float hue60 = this.h * 6f;
		float x = chroma * (1 - Abs(hue60 % 2 - 1));
		float r, g, b;

		switch (hue60) {
			case >= 0 and < 1:
				r = chroma;
				g = x;
				b = 0;
				break;
			case >= 1 and < 2:
				r = x;
				g = chroma;
				b = 0;
				break;
			case >= 2 and < 3:
				r = 0;
				g = chroma;
				b = x;
				break;
			case >= 3 and < 4:
				r = 0;
				g = x;
				b = chroma;
				break;
			case >= 4 and < 5:
				r = x;
				g = 0;
				b = chroma;
				break;
			default:
				r = chroma;
				g = 0;
				b = x;
				break;
		}

		double m = this.b - chroma;

		return new ColorRGB(
			r: (int)((r + m) * 255 + 0.5f),
			g: (int)((g + m) * 255 + 0.5f),
			b: (int)((b + m) * 255 + 0.5f)
		);
	}
}

record struct ColorHSL(float h, float s, float l) {
	public ColorHSL(ColorHS hs, float l) : this(hs.h, hs.s, l) { }

	public static ColorHSL FromRGB(int r, int g, int b) => FromRGB(r / 255f, g / 255f, b / 255f);

	public static ColorHSL FromRGB(float r, float g, float b) {
		float max = Max(r, Max(g, b));
		float min = Min(r, Min(g, b));
		float h, s, l;

		l = (max + min) / 2f;

		if (max == min) {
			h = s = 0;
		} else {
			float diff = max - min;

			s = l > 0.5f
				? diff / (2f - max - min)
				: diff / (max + min);

			if (max == r) {
				h = (g - b) / diff + (g < b ? 6 : 0);
			} else if (max == g) {
				h = (b - r) / diff + 2;
			} else {
				h = (r - g) / diff + 4;
			}

			h /= 6;
		}

		return new ColorHSL(h, s, l);
	}
}

#endregion

#region generators

sealed class SineWaveGenerator : Generator {
	public float Generate(int index, float x, float y) {
		return x >= 0.5 && y < 0.5
			? (Sin((y - 0.25f) * 2 * PI) + 1) / 2
			: 1;
	}
}

#endregion

#region color generators

sealed class RainbowColorGenerator : ColorGenerator {
	public ColorHS Generate(int index, float x, float y) {
		return new ColorHS(
			x,
			x < 0.5 && y < 0.5
				? y * 2
				: 1
		);
	}
}

#endregion
