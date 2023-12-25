using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace b_effort.b_led;

static class Color {
	public static HSB hsb(float h, float s = 1f, float b = 1f) {
		if (h != 1f) {
			h %= 1f;
			if (!float.IsNormal(h)) {
				h = 0f;
			}
		}
		return new HSB(h, s, b);
	}

	// ! This is sRGB
	public readonly record struct RGB(byte r, byte g, byte b, byte a = 255) {
		public RGB ContrastColor() => PerceivedLightness(this) < 0.5f
				? new RGB(255, 255, 255)
				: new RGB(0, 0, 0);

		[Impl(Inline)] public uint ToU32() {
			return (uint)(
				(this.r << 0)
			  | (this.g << 8)
			  | (this.b << 16)
			  | (this.a << 24)
			);
		}

		public static explicit operator rlColor(RGB @this) => new(@this.r, @this.g, @this.b, @this.a);
	}

	[DataContract]
	public record struct HSB(float h, float s, float b) {
		[DataMember] public float h = h;
		[DataMember] public float s = s;
		[DataMember] public float b = b;

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

		public static implicit operator HSB(Vector3 vec) => new(vec.X, vec.Y, vec.Z);
		public static implicit operator Vector3(HSB @this) => new(@this.h, @this.s, @this.b);

		public static implicit operator HSB(Vector4 vec) => new(vec.X, vec.Y, vec.Z);
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

	public static byte f32_to_int8(float value) => (byte)(value * 255 + 0.5f);

	// https://stackoverflow.com/questions/596216/formula-to-determine-perceived-brightness-of-rgb-color/56678483#56678483
	static float sRGB_to_lin(byte value) {
		float v = value / 255f;

		return v <= 0.04045
			? v / 12.92f
			: BMath.pow((v + 0.055f) / 1.055f, 2.4f);
	}

	// Y in CIEXYZ
	static float RelativeLuminance(RGB color)
		=> 0.2126f * sRGB_to_lin(color.r)
		 + 0.7152f * sRGB_to_lin(color.g)
		 + 0.0722f * sRGB_to_lin(color.b);

	// L* in CIELAB
	static float PerceivedLightness(RGB color) {
		float y = RelativeLuminance(color);
		float lStar = y <= 216 / 24389f
			? y * (24389 / 27f)
			: BMath.pow(y, 1 / 3f) * 116 - 16;

		return lStar / 100f;
	}
}

[DataContract]
sealed class Gradient {
	[DataContract]
	public sealed record Point(float pos, HSB color) : IComparable<Point> {
		[DataMember] public float pos = pos;
		[DataMember] public HSB color = color;

		public int CompareTo(Point? other) => other is null ? 1 : this.pos.CompareTo(other.pos);

		public static implicit operator float(Point @this) => @this.pos;
	}

	readonly List<Point> points;
	[DataMember] public IReadOnlyList<Point> Points => this.points;

	public Gradient() : this(
		new List<Point> {
			new(0f, hsb(0.0f, 0, 0)),
			new(1f, hsb(0.0f, 0)),
		}
	) { }

	[JsonConstructor]
	public Gradient(IReadOnlyList<Point> points) {
		if (points.Count < 2)
			throw new ArgumentOutOfRangeException(nameof(points), points.Count, "Must have at least 2 points");
		if (points[0] != 0f)
			throw new ArgumentOutOfRangeException(nameof(points), points[0], "First point must have pos 0");
		if (points[^1] != 1f)
			throw new ArgumentOutOfRangeException(nameof(points), points[^1], "Last point must have pos 1");
		
		this.points = points.ToList();
	}

	[Impl(Inline)]
	public HSB ColorAt(float pos) {
		var (p1, p2) = this.GetNeighboringPoints(pos);
		var dist = (pos - p1) / (p2 - p1);

		return hsb(
			h: BMath.lerp(dist, p1.color.h, p2.color.h),
			s: BMath.lerp(dist, p1.color.s, p2.color.s),
			b: BMath.lerp(dist, p1.color.b, p2.color.b)
		);
	}

	[Impl(Inline)]
	public HSB MapColor(HSB input) {
		var color = this.ColorAt(input.h);
		return color with {
			s = color.s * input.s,
			b = color.b * input.b,
		};
	}

	[Impl(Inline)]
	(Point p1, Point p2) GetNeighboringPoints(float pos) {
		var points = this.points;
		for (var i = 1; i < points.Count; i++) {
			var p1 = points[i - 1];
			var p2 = points[i];

			if (pos <= p2) {
				return (p1, p2);
			}
		}

		throw new OopsiePoopsie($"{pos} not in gradient");
	}

	public int Add(float pos, HSB color) {
		int i = this.points.FindIndex(p => pos <= p);
		this.points.Insert(i, new Point(pos, color));
		return i;
	}

	public bool RemoveAt(int i) {
		// don't allow first or last point to be removed
		if (i <= 0 || this.points.Count - 1 <= i)
			return false;
		
		this.points.RemoveAt(i);
		return true;
	}

	public void Sort() {
		this.points.Sort();
	}
}

sealed class GradientPreview : IDisposable {
	public readonly Gradient gradient;
	readonly int resolution;
	readonly Texture2D texture;
	readonly rlColor[] pixels;

	public nint TextureId => (nint)this.texture.id;

	public GradientPreview(Gradient gradient, int resolution) {
		this.gradient = gradient;
		this.resolution = resolution;
		this.texture = RaylibUtil.CreateTexture(resolution, 1, out this.pixels);
		this.UpdateTexture();
	}
	
	~GradientPreview() => this.Dispose();

	public void Dispose() {
		rl.UnloadTexture(this.texture);
		GC.SuppressFinalize(this);
	}
	
	public void UpdateTexture() {
		var pixels = this.pixels;
		for (var x = 0; x < this.resolution; x++) {
			var color = this.gradient.MapColor(hsb((float)x / this.resolution));
			pixels[x] = (rlColor)color.ToRGB();
		}
		rl.UpdateTexture(this.texture, pixels);
	}

}

[DataContract]
sealed class Palette : ClipContents {
	[DataMember] public string Id { get; }

	[DataMember] public string name;
	[DataMember] public readonly Gradient gradient;
	
	public readonly GradientPreview preview;

	public Palette(string name = "new palette", Gradient? gradient = null)
		: this(
			id: Guid.NewGuid().ToString(),
			name,
			gradient ?? new Gradient()
		) { }

	[JsonConstructor]
	public Palette(string id, string name, Gradient gradient) {
		this.Id = id;
		this.name = name;
		this.gradient = gradient;
		this.preview = new GradientPreview(gradient, 128);
	}
}
