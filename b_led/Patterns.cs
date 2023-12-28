namespace b_effort.b_led.patterns;

using static BMath;
using static PatternScript;

sealed class TestPattern : Pattern {
	public TestPattern() {
		this.m1 = new Macro { Name = "threshold", Value = 0.1f };
	}

	protected override HSB Render(int i, float x, float y) {
		x *= 20;
		y *= 20;
		
		var h = (sin(x) + cos(t * 0.15f))
		  / (sin(y + sin(x * 5f) * this.m2) + sin(t * 0.5f));
		var t3 = 0.05f + this.m1;
		var diff = (t3 - abs(h)) / t3;
		var b = diff > 0.67f ? 1 : diff > 0f ? diff / 0.67f : 0f;

		h = h + 0.25f + beat.saw(16);

		return hsb(h, 1, b);

		// float h = float.Abs(x * (x % y) - y * (y % x));
		// return new Color.HSB(h + 0.5f, 1, 1);
	}
}

sealed class EdgeBurstPattern : Pattern {
	protected override HSB Render(int i, float x, float y) {
		float t1 = beat.triangle(2f);
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
