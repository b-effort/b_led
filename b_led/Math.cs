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

public record struct Bounds(Vector2 min, Vector2 max) {
	public Vector2 min = min;
	public Vector2 max = max;
}
