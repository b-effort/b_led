namespace b_effort.b_led;

static class BMath {
	public const float PI = MathF.PI;
	public const float TAU = PI * 2;
	public const float PI2 = TAU;

	[Impl(Inline)] public static float abs(float x) => MathF.Abs(x);

	[Impl(Inline)] public static float clamp(float x, float min = 0f, float max = 1f) => Math.Clamp(x, min, max);

	[Impl(Inline)] public static float lerp(float x, float min, float max) => (max - min) * x + min;

	[Impl(Inline)] public static float pow(float x, float exp) => MathF.Pow(x, exp);

	[Impl(Inline)] public static int nearestEven(float x) => ((int)x + 1) & ~1;
	// !todo: maybe wrong?
	[Impl(Inline)] public static int nearestOdd(float x) => nearestEven(x) - 1;

	[Impl(Inline)] public static float sign(float x) => MathF.Sign(x);


	[Impl(Inline)] public static float sqr(float x) => x * x;
	[Impl(Inline)] public static float sqrt(float x) => MathF.Sqrt(x);

	public static bool perfectSqrt(float x, out int sqrt) {
		float sqrtF = MathF.Sqrt(x);
		bool isPerfect = sqrtF % 1 == 0;
		sqrt = isPerfect ? (int)sqrtF : -1;
		return isPerfect;
	}

	// Trig
	[Impl(Inline)] public static float sin(float x) => MathF.Sin(x);
	[Impl(Inline)] public static float sin01(float x) => (sin(x * TAU) + 1) / 2;
	[Impl(Inline)] public static float tan(float x) => MathF.Tan(x);
	[Impl(Inline)] public static float sec(float x) => 1f / cos(x);

	// Trig complements
	[Impl(Inline)] public static float cos(float x) => MathF.Cos(x);
	[Impl(Inline)] public static float cos01(float x) => (cos(x * TAU) + 1) / 2;
	[Impl(Inline)] public static float cot(float x) => 1f / tan(x);
	[Impl(Inline)] public static float csc(float x) => 1f / sin(x);

	public static class fx { }
}

// https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Geometry/Rect.cs
public record struct Rect(float x, float y, float _width, float _height) {
	float _xMin = x;
	float _yMin = y;
	float _width = _width;
	float _height = _height;

	public Rect(Vector2 pos, Vector2 size) : this(
		x: pos.X,
		y: pos.Y,
		_width: size.X,
		_height: size.Y
	) { }

	public float X {
		get => this._xMin;
		set => this._xMin = value;
	}
	public float Y {
		get => this._yMin;
		set => this._yMin = value;
	}
	public float Width {
		get => this._width;
		set => this._width = value;
	}
	public float Height {
		get => this._height;
		set => this._height = value;
	}

	public Vector2 Pos {
		get => new(this._xMin, this._yMin);
		set {
			this._xMin = value.X;
			this._yMin = value.Y;
		}
	}
	public Vector2 Center {
		get => new(this._xMin + this._width / 2f, this._yMin + this._height / 2f);
		set {
			this._xMin = value.X - this._width / 2f;
			this._yMin = value.Y - this._height / 2f;
		}
	}
	public Vector2 Size {
		get => new(this._width, this._height);
		set {
			this._width = value.X;
			this._height = value.Y;
		}
	}

	public Vector2 Min {
		get => new(this.XMin, this.YMin);
		set {
			this.XMin = value.X;
			this.YMin = value.Y;
		}
	}
	public Vector2 Max {
		get => new(this.XMax, this.YMax);
		set {
			this.XMax = value.X;
			this.YMax = value.Y;
		}
	}

	public float XMin {
		get => this._xMin;
		set {
			float oldMax = this.XMax;
			this._xMin = value;
			this._width = oldMax - value;
		}
	}
	public float YMin {
		get => this._yMin;
		set {
			float oldMax = this.YMax;
			this._yMin = value;
			this._height = oldMax - value;
		}
	}
	public float XMax {
		get => this._xMin + this._width;
		set => this._width = value - this._xMin;
	}
	public float YMax {
		get => this._yMin + this._height;
		set => this._height = value - this._yMin;
	}

	public void Expand(Rect other) {
		if (other.XMin < this.XMin) this.XMin = other.XMin;
		if (other.XMax > this.XMax) this.XMax = other.XMax;
		if (other.YMin < this.YMin) this.YMin = other.YMin;
		if (other.YMax > this.YMax) this.YMax = other.YMax;
	}
}
