namespace b_effort.b_led;

sealed class Fixture_Matrix_64x64 : FixtureTemplate {
	public override Guid Id { get; } = new("d41d1982-e3c8-462a-b339-b417dbc08ed6");

	const int Width = 64;
	const int Height = Width;

	public Fixture_Matrix_64x64() : base(
		numLeds: Width * Height,
		mapper: LEDMappers.Matrix(Width, Height, 0.25f)
	) { }
}

static class LEDMappers {
	public static LEDMapper Matrix(int width, int height, float spacing) {
		return leds => {
			if (width * height != leds.Length)
				throw new OopsiePoopsie(
					$"Matrix dimensions are invalid for fixture. width={width} height={height} numLeds={leds.Length}"
				);

			for (int y = 0, i = 0; y < height; y++)
			for (var x = 0; x < width && i < leds.Length; x++) {
				leds[i] = vec2(x * spacing, y * spacing);
			}

			return leds;
		};
	}
}
