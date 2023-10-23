using System.Diagnostics;

namespace b_effort.b_led;

using static PatternScript;

static class PatternScript {
	static readonly Stopwatch timer = Stopwatch.StartNew();

	public const float PI = MathF.PI;

	public static float t => (float)timer.Elapsed.TotalSeconds;

	public static float sin(float x) => BMath.Sin01(x);
	public static float cos(float x) => BMath.Cos01(x);

	public static class osc {
		public static float saw(float interval) => (t % interval) / interval;
		public static float sine(float interval) => sin(saw(interval));
	}
}

sealed class HSBDemoPattern : Pattern, ColorPattern {
	Color.B Pattern.Generate(int index, float x, float y) {
		return x >= 0.5 && y < 0.5
			? sin(y - 0.25f)
			: 1;
	}

	Color.HS ColorPattern.Generate(int index, float x, float y) {
		return new Color.HS(
			x,
			x < 0.5 && y < 0.5
				? y * 2
				: 1
		);
	}
}

sealed class SinPattern : Pattern {
	Color.B Pattern.Generate(int index, float x, float y) {
		return sin(x + t);
	}
}
