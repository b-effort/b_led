namespace b_effort.b_led;

static class Color {
	public static HSB hsb(float h, float s, float b) => new(h, s, b);

	public static byte f32_to_int8(float value) => (byte)(value * 255 + 0.5f);

	public readonly record struct RGB(byte r, byte g, byte b, byte a = 255) {
		[Impl(Inline)] public uint ToU32() {
			return (uint)(
				(this.r << 0)
			  | (this.g << 8)
			  | (this.b << 16)
			  | (this.a << 24)
			);
		}


		public static implicit operator rlColor(RGB @this) => new(@this.r, @this.g, @this.b, @this.a);
	}

	public readonly record struct HSB(float h, float s, float b) {
		[Impl(Inline)] public RGB ToRGB(float a = 1) {
			if (this.s == 0) {
				var value = f32_to_int8(this.b);
				return new RGB(value, value, value, f32_to_int8(a));
			}

			float chroma = this.b * this.s;
			float hue60 = this.h * 6f;
			float x = chroma * (1 - MathF.Abs(hue60 % 2 - 1));
			float r, g, b;

			switch (hue60) {
				case >= 0 and < 1:
					r = chroma;
					g = x;
					b = 0;
					break;
				case >= 1 and < 2:
					r = x;
					g = chroma;
					b = 0;
					break;
				case >= 2 and < 3:
					r = 0;
					g = chroma;
					b = x;
					break;
				case >= 3 and < 4:
					r = 0;
					g = x;
					b = chroma;
					break;
				case >= 4 and < 5:
					r = x;
					g = 0;
					b = chroma;
					break;
				default:
					r = chroma;
					g = 0;
					b = x;
					break;
			}

			float m = this.b - chroma;

			return new RGB(
				r: f32_to_int8(r + m),
				g: f32_to_int8(g + m),
				b: f32_to_int8(b + m),
				a: f32_to_int8(a)
			);
		}

		[Impl(Inline)] public uint ToU32() => this.ToRGB().ToU32();

		public static implicit operator Vector4(HSB @this) => new(@this.h, @this.s, @this.b, 1f);
	}

	public readonly record struct HSL(float h, float s, float l) {
		public static HSL FromRGB(RGB rgb) => FromRGB(rgb.r / 255f, rgb.g / 255f, rgb.b / 255f);

		public static HSL FromRGB(float r, float g, float b) {
			float max = MathF.Max(r, MathF.Max(g, b));
			float min = MathF.Min(r, MathF.Min(g, b));
			float h, s, l;

			l = (max + min) / 2f;

			if (max == min) {
				h = s = 0;
			} else {
				float diff = max - min;

				s = l > 0.5f
					? diff / (2f - max - min)
					: diff / (max + min);

				if (max == r) {
					h = (g - b) / diff + (g < b ? 6 : 0);
				} else if (max == g) {
					h = (b - r) / diff + 2;
				} else {
					h = (r - g) / diff + 4;
				}

				h /= 6;
			}

			return new HSL(h, s, l);
		}
	}
}
