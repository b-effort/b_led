using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace b_effort.b_led;

using static BMath;
using static PatternScript;

static class BMath {
	public const float PI = MathF.PI;
	public const float TAU = PI * 2;
	public const float PI2 = TAU;

	public static float abs(float x) => MathF.Abs(x);
	public static float clamp(float x, float min = 0f, float max = 1f) => Math.Clamp(x, min, max);
	public static float sign(float x) => MathF.Sign(x);

	public static float sqr(float x) => x * x;
	public static float sqrt(float x) => MathF.Sqrt(x);

	// Trig
	public static float sin(float x) => MathF.Sin(x);
	public static float sin01(float x) => (sin(x * TAU) + 1) / 2;
	public static float tan(float x) => MathF.Tan(x);
	public static float sec(float x) => 1f / cos(x);

	// Trig complements
	public static float cos(float x) => MathF.Cos(x);
	public static float cos01(float x) => (cos(x * TAU) + 1) / 2;
	public static float cot(float x) => 1f / tan(x);
	public static float csc(float x) => 1f / sin(x);

	public static class fx { }
}

record struct Tempo(float bpm) {
	public static readonly Tempo Zero = 0f;

	public float bpm = bpm;

	public const int MinUsableBPM = 20;
	public bool IsUsable => this.bpm >= MinUsableBPM;
	public float BeatsPerSecond => this.bpm / 60;
	public float SecondsPerBeat => 1f / this.BeatsPerSecond;

	public static Tempo FromBeatDuration(TimeSpan secondsPerBeat) =>
		new((float)(60 / secondsPerBeat.TotalSeconds));

	public static implicit operator float(Tempo @this) => @this.bpm;
	public static implicit operator Tempo(float bpm) => new(bpm);

	public static implicit operator bool(Tempo @this) => @this != Zero;

	public Tempo Rounded() => MathF.Round(this.bpm, MidpointRounding.ToEven);
}

static class Metronome {
	public static readonly Stopwatch timer = Stopwatch.StartNew();

	public static Tempo tempo = 128;
	public static float speed = 1f;
	static float lastBeatProgress = 0f;

	public static TimeSpan Elapsed => timer.Elapsed;
	public static float DownbeatOffset { get; private set; }
	public static float T => ((float)Elapsed.TotalSeconds - DownbeatOffset) * tempo.BeatsPerSecond * speed;
	public static float BeatProgress => T % 1f;
	public static bool IsOnBeat { get; private set; }
	public static float TLastBeat { get; private set; }
	public static float BeatPulse => IsOnBeat ? 1f : 0f;

	public static void Tick() {
		IsOnBeat = BeatProgress < lastBeatProgress;
		if (IsOnBeat) {
			TLastBeat = T;
		}
		lastBeatProgress = BeatProgress;
	}

	public static void SetDownbeat() {
		DownbeatOffset = BeatProgress;
		Tick();
	}

	public static float Interval(float beats) => t % beats / beats;

#region tap tempo

	static readonly TimeSpan TapResetTime = new(0, 0, seconds: 2);
	const int MinTaps = 4;

	static readonly Stopwatch tapTimer = Stopwatch.StartNew();
	static TimeSpan tappingTime = -TapResetTime;
	public static int tapCounter = 0;

	public static Tempo TapTempoRealtime
		=> tapCounter > 1
			? Tempo.FromBeatDuration(tappingTime / (tapCounter - 1))
			: Tempo.Zero;

	public static Tempo TapTempo
		=> tapCounter >= MinTaps
		&& TapTempoRealtime.IsUsable
			? TapTempoRealtime.Rounded()
			: Tempo.Zero;

	static TimeSpan SinceLastTap => tapTimer.Elapsed - tappingTime;

	public static void Tap(bool apply = true) {
		if (SinceLastTap >= TapResetTime) {
			tapCounter = 0;
			tappingTime = TimeSpan.Zero;
			tapTimer.Restart();
		} else {
			tappingTime = tapTimer.Elapsed;
		}
		tapCounter++;

		if (apply && TapTempo) {
			tempo = TapTempo;
			SetDownbeat();
		}
	}

#endregion
}

static class PatternScript {
	public static float t => Metronome.T;
	public static float interval(float beats) => t % beats / beats;

	public static float saw(float x) => x % 1f;
	public static float sine(float x) => sin01(x - 0.25f);
	public static float triangle(float x) {
		float x2 = x % 1 * 2;
		return x2 <= 1f ? x2 : 2f - x2;
	}
	public static float square(float x) => pulse(x, 0.5f);
	public static float pulse(float x, float dutyCycle) => x % 1 >= 1 - dutyCycle % 1 ? 1f : 0f;

	[SuppressMessage("ReSharper", "MemberHidesStaticFromOuterClass")]
	public static class beat {
		public static float saw(float beats = 1f) => interval(beats);
		public static float sine(float beats) => PatternScript.sine(interval(beats));
		public static float triangle(float beats) => PatternScript.triangle(interval(beats));
		public static float square(float beats) => PatternScript.square(interval(beats));
		public static float pulse(float beats, float dutyCycle) => PatternScript.pulse(interval(beats), dutyCycle);
	}
}

abstract class Pattern {
	const int BufferWidth = State.BufferWidth;

	public readonly Color.HSB[,] pixels = new Color.HSB[BufferWidth, BufferWidth];

	public void Update(float dt) {
		this.PreRender(dt);
		for (var y = 0; y < BufferWidth; y++) {
			for (var x = 0; x < BufferWidth; x++) {
				int i = y * BufferWidth + x;
				const float lengthMinusOne = BufferWidth - 1f;
				float x01 = x / lengthMinusOne;
				float y01 = y / lengthMinusOne;

				this.pixels[y, x] = this.Render(i, x01, y01);
			}
		}
	}

	protected virtual void PreRender(float dt) { }
	protected abstract Color.HSB Render(int i, float x, float y);
}

sealed class TestPattern : Pattern {
	protected override Color.HSB Render(int i, float x, float y) {
		x -= 0.5f;
		y -= 0.5f;
		x *= 3;
		y *= 3;

		var h = (sin(x * 10) + cos(t * 0.48f))
		  / (sin(y * 10) + sin(t * 0.5f)) + beat.saw();
		var t3 = t * 0.1f;
		var b = abs(h) < t3 ? 1 : 0;

		h = h + 0.25f + beat.sine(60) * 0.5f;

		return new Color.HSB(h, 1, b);

		// float h = float.Abs(x * (x % y) - y * (y % x));
		// return new Color.HSB(h + 0.5f, 1, 1);
	}
}

sealed class EdgeBurstPattern : Pattern {
	protected override Color.HSB Render(int i, float x, float y) {
		float t1 = beat.triangle(5f);
		float edge = clamp(triangle(x) + t1 * 4 - 2);
		float h = edge * edge - 0.2f;
		float b = triangle(edge);

		return new Color.HSB(h, 1, b);
	}
}

sealed class HSBDemoPattern : Pattern {
	protected override Color.HSB Render(int i, float x, float y) {
		return new Color.HSB(
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
