using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace b_effort.b_led;

static class PatternScript {
	public static float t { [Impl(Inline)] get => Metronome.T; }
	public static float dt => Metronome.TDelta;
	public static float interval(float beats) => Metronome.Interval(beats);

	public static float saw(float x) => x % 1f;
	public static float sine(float x) => BMath.sin01(x - 0.25f);
	public static float triangle(float x) {
		float x2 = x % 1 * 2;
		return x2 <= 1f ? x2 : 2f - x2;
	}
	public static float square(float x) => pulse(x + 0.5f, 0.5f);
	public static float pulse(float x, float dutyCycle) => x % 1 >= 1 - (dutyCycle % 1) ? 1f : 0f;

	[SuppressMessage("ReSharper", "MemberHidesStaticFromOuterClass")]
	public static class beat {
		public static float saw(float beats = 1f, float phase = 0f) => PatternScript.saw(interval(beats) + phase);
		public static float sine(float beats, float phase = 0f) => PatternScript.sine(interval(beats) + phase);
		public static float triangle(float beats, float phase = 0f) => PatternScript.triangle(interval(beats) + phase);
		public static float square(float beats, float phase = 0f) => PatternScript.square(interval(beats) + phase);
		public static float pulse(float beats, float dutyCycle, float phase = 0f) => PatternScript.pulse(interval(beats) + phase, dutyCycle);
	}
}

sealed class Macro {
	float value;
	public float Value {
		[Impl(Inline)] get => this.value;
		set => this.value = BMath.clamp(value, this.Min, this.Max);
	}

	public required string Name { get; init; }
	public float Min { get; init; } = 0f;
	public float Max { get; init; } = 1f;
	public float Range => this.Max - this.Min;

	public static implicit operator float(Macro @this) => @this.value;

	public static readonly Macro scaleX = new() { Name = "scale x", Value = 1f, Min = 0.1f, Max = 10f };
	public static readonly Macro scaleY = new() { Name = "scale y", Value = 1f, Min = 0.1f, Max = 10f };
	public static readonly Macro hue_offset = new() { Name = "hue", Min = -5f, Max = 5f };

	static Macro[]? global;
	public static IReadOnlyList<Macro> Global => global
		??= new[] { scaleX, scaleY, hue_offset };
}

[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature, ImplicitUseTargetFlags.WithInheritors)]
abstract class Pattern : ClipContents, IDisposable {
	public string Id => this.name;

	public readonly string name;
	
	public Macro m1 = new() { Name = "macro 1" };
	public Macro m2 = new() { Name = "macro 2" };
	public Macro m3 = new() { Name = "macro 3" };
	public Macro m4 = new() { Name = "macro 4" };

	Macro[]? macros;
	public IReadOnlyList<Macro> Macros => this.macros
		??= new[] { this.m1, this.m2, this.m3, this.m4 };

	public const int Width = State.BufferWidth;
	public const int Height = State.BufferWidth;
	public readonly HSB[,] pixels;

	readonly Texture2D texture;
	readonly rlColor[] texturePixels;

	public nint TextureId => (nint)this.texture.id;

	protected Pattern() {
		this.name = this.GetType().Name.Replace("Pattern", null);
		this.pixels = new HSB[Width, Width];
		this.texture = RaylibUtil.CreateTexture(Width, Height, out this.texturePixels);
	}

	~Pattern() => this.Dispose();

	public void Dispose() {
		rl.UnloadTexture(this.texture);
		GC.SuppressFinalize(this);
	}

	public void Update() {
		this.PreRender();

		float scaleX = Macro.scaleX.Value;
		float scaleY = Macro.scaleY.Value;

		HSB[,] pixels = this.pixels;
		for (var y = 0; y < Width; y++)
		for (var x = 0; x < Width; x++) {
			int i = y * Width + x;
			const float lengthMinusOne = Width - 1f;
			float x01 = x / lengthMinusOne * scaleX;
			float y01 = y / lengthMinusOne * scaleY;

			pixels[y, x] = this.Render(i, x01, y01);
			this.texturePixels[y * Width + x] = (rlColor)pixels[y, x].ToRGB();
		}

		rl.UpdateTexture(this.texture, this.texturePixels);
	}

	protected virtual void PreRender() { }
	protected abstract HSB Render(int i, float x, float y);
}
