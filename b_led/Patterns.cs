using System.Diagnostics.CodeAnalysis;

namespace b_effort.b_led;

using static BMath;
using static PatternScript;

static class BMath {
	public const float PI = MathF.PI;
	public const float TAU = PI * 2;
	public const float PI2 = TAU;

	[Impl(Inline)] public static float abs(float x) => MathF.Abs(x);

	[Impl(Inline)] public static float clamp(float x, float min = 0f, float max = 1f) => Math.Clamp(x, min, max);

	[Impl(Inline)] public static float lerp(float x, float min, float max) => (max - min) * x + min;

	[Impl(Inline)] public static float pow(float x, float exp) => MathF.Pow(x, exp);

	[Impl(Inline)] public static int nearestEven(float x) => ((int)x + 1) & ~1;
	// !todo: maybe wrong?
	[Impl(Inline)] public static int nearestOdd(float x) => nearestEven(x) - 1;

	[Impl(Inline)] public static float sign(float x) => MathF.Sign(x);


	[Impl(Inline)] public static float sqr(float x) => x * x;
	[Impl(Inline)] public static float sqrt(float x) => MathF.Sqrt(x);

	// Trig
	[Impl(Inline)] public static float sin(float x) => MathF.Sin(x);
	[Impl(Inline)] public static float sin01(float x) => (sin(x * TAU) + 1) / 2;
	[Impl(Inline)] public static float tan(float x) => MathF.Tan(x);
	[Impl(Inline)] public static float sec(float x) => 1f / cos(x);

	// Trig complements
	[Impl(Inline)] public static float cos(float x) => MathF.Cos(x);
	[Impl(Inline)] public static float cos01(float x) => (cos(x * TAU) + 1) / 2;
	[Impl(Inline)] public static float cot(float x) => 1f / tan(x);
	[Impl(Inline)] public static float csc(float x) => 1f / sin(x);

	public static class fx { }
}

static class PatternScript {
	public static float t => Metronome.T;
	public static float dt => Metronome.TDelta;
	public static float interval(float beats) => Metronome.Interval(beats);

	public static float saw(float x) => x % 1f;
	public static float sine(float x) => sin01(x - 0.25f);
	public static float triangle(float x) {
		float x2 = x % 1 * 2;
		return x2 <= 1f ? x2 : 2f - x2;
	}
	public static float square(float x) => pulse(x, 0.5f);
	public static float pulse(float x, float dutyCycle) => x % 1 >= 1 - dutyCycle % 1 ? 1f : 0f;

	[SuppressMessage("ReSharper", "MemberHidesStaticFromOuterClass")]
	public static class beat {
		public static float saw(float beats = 1f) => interval(beats);
		public static float sine(float beats) => PatternScript.sine(interval(beats));
		public static float triangle(float beats) => PatternScript.triangle(interval(beats));
		public static float square(float beats) => PatternScript.square(interval(beats));
		public static float pulse(float beats, float dutyCycle) => PatternScript.pulse(interval(beats), dutyCycle);
	}
}

#region macros

public sealed class Macro {
	float value;
	public float Value {
		[Impl(Inline)] get => this.value;
		set => this.value = clamp(value, this.Min, this.Max);
	}

	public required string Name { get; init; }
	public float Min { get; init; } = 0f;
	public float Max { get; init; } = 1f;
	public float Range => this.Max - this.Min;

	public static implicit operator float(Macro @this) => @this.value;

	public static readonly Macro scaleX = new() { Name = "scale x", Value = 0.5f, Min = 0.1f, Max = 10f };
	public static readonly Macro scaleY = new() { Name = "scale y", Value = 0.5f, Min = 0.1f, Max = 10f };
	public static readonly Macro hue_offset = new() { Name = "hue", Min = -5f, Max = 5f };

	static Macro[]? global;
	public static IReadOnlyList<Macro> Global => global
		??= new[] { scaleX, scaleY, hue_offset };
}

#endregion

abstract class Pattern {
	public Macro m1 = new() { Name = "macro 1" };
	public Macro m2 = new() { Name = "macro 2" };
	public Macro m3 = new() { Name = "macro 3" };
	public Macro m4 = new() { Name = "macro 4" };

	Macro[]? macros;
	public IReadOnlyList<Macro> Macros => this.macros
		??= new[] { this.m1, this.m2, this.m3, this.m4 };

	const int BufferWidth = State.BufferWidth;
	public readonly HSB[,] pixels = new HSB[BufferWidth, BufferWidth];

	public void Update() {
		this.PreRender();

		var scaleX = Macro.scaleX.Value;
		var scaleY = Macro.scaleY.Value;

		for (var y = 0; y < BufferWidth; y++) {
			for (var x = 0; x < BufferWidth; x++) {
				int i = y * BufferWidth + x;
				const float lengthMinusOne = BufferWidth - 1f;
				float x01 = x / lengthMinusOne * scaleX;
				float y01 = y / lengthMinusOne * scaleY;

				this.pixels[y, x] = this.Render(i, x01, y01);
			}
		}
	}

	protected virtual void PreRender() { }
	protected abstract HSB Render(int i, float x, float y);
}

sealed class TestPattern : Pattern {
	public TestPattern() {
		this.m1 = new Macro { Name = "threshold", Value = 0.1f };
	}

	protected override HSB Render(int i, float x, float y) {
		x -= 0.5f;
		y -= 0.5f;
		x *= 5 + beat.saw(24 * 2) * 50;
		y *= 5 + beat.saw(40 * 2) * 50;

		var h = (sin(x) + cos(t * 0.15f))
		  / (sin(y + sin(x * 5f) * this.m2) + sin(t * 0.5f)) + beat.saw(4);
		var t3 = beat.saw(4) * 0.5f + this.m1;
		var b = abs(h) < t3 ? 1 : 0;

		h = h + 0.25f + beat.saw(2);

		return hsb(h, 1, b);

		// float h = float.Abs(x * (x % y) - y * (y % x));
		// return new Color.HSB(h + 0.5f, 1, 1);
	}
}

sealed class EdgeBurstPattern : Pattern {
	protected override HSB Render(int i, float x, float y) {
		float t1 = beat.triangle(5f);
		float edge = clamp(triangle(x) + t1 * 4 - 2);
		float h = edge * edge - 0.2f;
		float b = triangle(edge);

		return hsb(h, 1, b);
	}
}

sealed class HSBDemoPattern : Pattern {
	protected override HSB Render(int i, float x, float y) {
		return hsb(
			h: x,
			s: x < 0.5 && y < 0.5
				? y * 2
				: 1,
			b: x >= 0.5 && y < 0.5
				? sin01(y - 0.25f)
				: 1
		);
	}
}
