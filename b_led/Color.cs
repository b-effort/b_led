using System.Runtime.InteropServices;

namespace b_effort.b_led;

static class Color {
	public static HSB hsb(float h, float s, float b) => new(h, s, b);

	[StructLayout(LayoutKind.Sequential, Size = 4)]
	public readonly record struct RGB(byte r, byte g, byte b) {
		public static implicit operator rlColor(RGB @this) => new(@this.r, @this.g, @this.b, (byte)255);
	}

	public readonly record struct B(float b) {
		public static implicit operator B(float b) => new(b);
		public static implicit operator float(B @this) => @this.b;
	}

	public readonly record struct HS(float h, float s = 1);

	public readonly record struct HSB(float h, float s, float b) {
		public HSB(HS hs, B b) : this(hs.h, hs.s, b) { }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public RGB ToRGB() {
			if (this.s == 0) {
				var value = (byte)(this.b * 255 + 0.5f);
				return new RGB(value, value, value);
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

			double m = this.b - chroma;

			return new RGB(
				r: (byte)((r + m) * 255 + 0.5f),
				g: (byte)((g + m) * 255 + 0.5f),
				b: (byte)((b + m) * 255 + 0.5f)
			);
		}

		public static implicit operator Vector4(HSB @this) => new(@this.h, @this.s, @this.b, 1f);
	}

	public readonly record struct HSL(float h, float s, float l) {
		public HSL(HS hs, float l) : this(hs.h, hs.s, l) { }

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
