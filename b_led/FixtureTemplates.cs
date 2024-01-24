namespace b_effort.b_led;

sealed class Fixture_Matrix_64x64 : FixtureTemplate {
	public Fixture_Matrix_64x64() : base(
		id: new("d41d1982-e3c8-462a-b339-b417dbc08ed6"),
		mapper: PixelMappers.Matrix(0.25f)
	) { }
}

static class PixelMappers {
	public static PixelMapper Matrix(float spacing) {
		return pixels => {
			if (!BMath.perfectSqrt(pixels.Length, out int size))
				throw new OopsiePoopsie(
					$"Invalid num leds for matrix, not a perfect square. numLeds={pixels.Length}"
				);

			for (int y = 0, i = 0; y < size; y++)
			for (var x = 0; x < size && i < pixels.Length; x++) {
				pixels[i] = vec2(x * spacing, y * spacing);
			}

			return pixels;
		};
	}
}
