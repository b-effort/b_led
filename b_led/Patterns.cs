using System.Diagnostics;

namespace b_effort.b_led;

using static BMath;
using static PatternScript;

abstract class Pattern {
	const int BufferWidth = State.BufferWidth;
	protected const int PixelCount = BufferWidth * BufferWidth;

	public readonly Color.HSB[,] pixels = new Color.HSB[BufferWidth, BufferWidth];

	public void Update(float dt) {
		this.PreRender(dt);
		for (var y = 0; y < BufferWidth; y++) {
			for (var x = 0; x < BufferWidth; x++) {
				int i = y * BufferWidth + x;
				const float lengthMinusOne = BufferWidth - 1f;
				float x01 = x / lengthMinusOne;
				float y01 = y / lengthMinusOne;

				this.pixels[y, x] = this.Render(i, x01, y01);
			}
		}
	}

	protected virtual void PreRender(float dt) { }
	protected abstract Color.HSB Render(int i, float x, float y);
}

static class BMath {
	public const float PI = MathF.PI;
	public const float PI2 = PI * 2;

	public static float sqr(float x) => x * x;
	public static float sqrt(float x) => MathF.Sqrt(x);

	public static float clamp(float x, float min = 0f, float max = 1f) => Math.Clamp(x, min, max);

	public static float sin(float x) => MathF.Sin(x);
	public static float sin01(float x) => (sin(x * PI2) + 1) / 2;
	public static float tan(float x) => MathF.Tan(x);
	public static float sec(float x) => 1f / cos(x);

	public static float cos(float x) => MathF.Cos(x);
	public static float cos01(float x) => (cos(x * PI2) + 1) / 2;
	public static float cot(float x) => 1f / tan(x);
	public static float csc(float x) => 1f / sin(x);

	public static class fx { }
}

static class PatternScript {
	static readonly Stopwatch timer = Stopwatch.StartNew();

	public static TimeSpan tspan => timer.Elapsed;
	public static float t => (float)tspan.TotalSeconds;

	public static float triangle(float x) {
		float x2 = x * 2;
		return x2 <= 1f ? x2 : 2 - x2;
	}

	public static class wave {
		public static float saw(float interval) {
			float intervalMs = interval * 1000;
			return (float)((tspan.TotalMilliseconds % intervalMs) / intervalMs);
		}
		public static float sine(float interval) => sin01(saw(interval));
		// ReSharper disable once MemberHidesStaticFromOuterClass
		public static float triangle(float interval) => PatternScript.triangle(saw(interval));
	}
}

sealed class TestPattern : Pattern {
	protected override Color.HSB Render(int i, float x, float y) {
		x -= 0.5f;
		y -= 0.5f;
		x *= 3;
		y *= 3;

		float xy = x / y;
		var sign = MathF.Sign(xy);
		float wav = wave.sine(sign * clamp(float.Abs(xy), 0.5f, 2f));
		// float wav = 1;
		var h = (sin(x * 10) + cos(t * 0.9f)) / (sin(y * 10) + sin(t)) * wav;
		var b = h is < 0.1f and > -0.5f ? 1 : 0;

		h = h + 0.25f + wave.sine(10) * 0.5f;

		return new Color.HSB(h, 1, b);

		// float h = float.Abs(x * (x % y) - y * (y % x));
		// return new Color.HSB(h + 0.5f, 1, 1);
	}
}

sealed class EdgeBurstPattern : Pattern {
	protected override Color.HSB Render(int i, float x, float y) {
		float t1 = wave.triangle(5f);
		float edge = clamp(triangle(x) + t1 * 4 - 2);
		float h = edge * edge - 0.2f;
		float b = triangle(edge);

		return new Color.HSB(h, 1, b);
	}
}

sealed class HSBDemoPattern : Pattern {
	protected override Color.HSB Render(int i, float x, float y) {
		return new Color.HSB(
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
