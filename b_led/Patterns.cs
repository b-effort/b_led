using System.Diagnostics;

namespace b_effort.b_led;

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

static class PatternScript {
	static readonly Stopwatch timer = Stopwatch.StartNew();

	public const float PI = MathF.PI;

	public static float t => (float)timer.Elapsed.TotalSeconds;

	public static float sqr(float x) => x * x;
	public static float sqrt(float x) => MathF.Sqrt(x);
	public static float clamp(float x, float min = 0f, float max = 1f) => BMath.clamp(x, min, max);

	public static float sin(float x) => BMath.sin01(x);
	public static float cos(float x) => BMath.cos01(x);
	public static float triangle(float x) {
		float x2 = x * 2;
		return x2 <= 1f ? x2 : 2 - x2;
	}

	public static class wave {
		public static float saw(float interval) => (t % interval) / interval;
		public static float sine(float interval) => sin(saw(interval));
		// ReSharper disable once MemberHidesStaticFromOuterClass
		public static float triangle(float interval) => PatternScript.triangle(saw(interval));
	}
}

sealed class TestPattern : Pattern {
	protected override Color.HSB Render(int i, float x, float y) {
		float h = BMath.sin(x * 10) / BMath.sin(y * 10) * wave.saw(y);

		return new Color.HSB(h + 0.5f, 1, h < wave.sine(1) ? 1 : 0);
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
				? sin(y - 0.25f)
				: 1
		);
	}
}
