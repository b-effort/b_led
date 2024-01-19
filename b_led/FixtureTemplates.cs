namespace b_effort.b_led;

sealed class Fixture_Matrix_64x64 : FixtureTemplate {
	public override Guid Id { get; } = new("d41d1982-e3c8-462a-b339-b417dbc08ed6");

	public Fixture_Matrix_64x64() : base(
		mapper: LEDMappers.Matrix(0.25f)
	) { }
}

static class LEDMappers {
	public static LEDMapper Matrix(float spacing) {
		return leds => {
			if (!BMath.perfectSqrt(leds.Length, out int size))
				throw new OopsiePoopsie(
					$"Invalid num leds for matrix, not a perfect square. numLeds={leds.Length}"
				);

			for (int y = 0, i = 0; y < size; y++)
			for (var x = 0; x < size && i < leds.Length; x++) {
				leds[i] = vec2(x * spacing, y * spacing);
			}

			return leds;
		};
	}
}
