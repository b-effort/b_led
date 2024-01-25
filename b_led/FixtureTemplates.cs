namespace b_effort.b_led;

sealed class Fixture_Default : FixtureTemplate {
	public Fixture_Default() : base(
		id: new("2ef0b00d-2d88-4944-90db-b684060f218c"),
		mapper: LEDMappers.Default
	) { }
}

sealed class Fixture_Matrix_64x64 : FixtureTemplate {
	public Fixture_Matrix_64x64() : base(
		id: new("d41d1982-e3c8-462a-b339-b417dbc08ed6"),
		mapper: LEDMappers.Matrix(0.25f)
	) { }
}

static class LEDMappers {
	public static readonly LEDMapper Default = coords => { };

	public static LEDMapper Matrix(float spacing) {
		return coords => {
			if (!BMath.perfectSqrt(coords.Length, out int size))
				throw new OopsiePoopsie($"Invalid num leds for matrix, not a perfect square. numLeds={coords.Length}");

			for (int y = 0, i = 0; y < size; y++)
			for (var x = 0; x < size && i < coords.Length; x++, i++) {
				coords[i] = vec2(x * spacing, y * spacing);
			}
		};
	}
}
