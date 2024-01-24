namespace b_effort.b_led.patterns;

using static BMath;
using static PatternScript;

sealed class Pattern_Demo : Pattern {
	public Pattern_Demo() : base(id: new("0579a129-4dcf-4cf3-b392-92306d53c96d")) {
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

sealed class Pattern_EdgeBurst : Pattern {
	public Pattern_EdgeBurst() : base(id: new("3a42dd0f-4147-4e96-88b4-b834401c20fe")) { }

	protected override HSB Render(int i, float x, float y) {
		float t1 = beat.triangle(2f);
		float edge = clamp(triangle(x) + t1 * 4 - 2);
		float h = edge * edge - 0.2f;
		float b = triangle(edge);

		return hsb(h, 1, b);
	}
}

sealed class Pattern_Test_HSB : Pattern {
	public Pattern_Test_HSB() : base(id: new("4b3a479f-e41d-4541-a778-734b321d0917")) { }

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
