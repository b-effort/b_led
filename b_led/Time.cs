using System.Diagnostics;

namespace b_effort.b_led;

record struct Tempo(float bpm) {
	public static readonly Tempo Zero = 0f;

	public float bpm = bpm;

	public const int MinUsableBPM = 20;
	public const int MaxUsableBPM = 220;
	public bool IsUsable => this.bpm is >= MinUsableBPM and <= MaxUsableBPM;
	public float BeatsPerSecond => this.bpm / 60;
	public float SecondsPerBeat => 1f / this.BeatsPerSecond;

	public static Tempo FromBeatDuration(TimeSpan secondsPerBeat) =>
		new((float)(60 / secondsPerBeat.TotalSeconds));

	public static implicit operator float(Tempo @this) => @this.bpm;
	public static implicit operator Tempo(float bpm) => new(bpm);

	public static implicit operator bool(Tempo @this) => @this != Zero;

	public Tempo Rounded() => MathF.Round(this.bpm, MidpointRounding.ToEven);
}

readonly record struct Phase(float value) {
	public readonly float value = value % 1;

	public static explicit operator Phase(float value) => new(value);
	public static implicit operator float(Phase @this) => @this.value;
}

static class Metronome {
	public static readonly Stopwatch timer = Stopwatch.StartNew();

	public static Tempo tempo = 128;
	public static float speed = 1f;
	static float beatPhaseLast = 0f;

	public static TimeSpan Elapsed => timer.Elapsed;
	public static float T { get; private set; }
	public static float TLive => ((float)Elapsed.TotalSeconds - DownbeatPhase) * tempo.BeatsPerSecond * speed;
	public static float TDelta { get; private set; }
	public static float TLast { get; private set; }
	public static float TLastBeat { get; private set; }

	public static bool IsOnBeat { get; private set; }
	public static Phase DownbeatPhase { get; private set; }
	public static Phase BeatPhase => (Phase)T;
	public static float BeatPulse => IsOnBeat ? 1f : 0f;

	public static void Tick() {
		T = TLive;
		TDelta = T - TLast;
		TLast = T;

		IsOnBeat = BeatPhase < beatPhaseLast;
		beatPhaseLast = BeatPhase;
		if (IsOnBeat) {
			TLastBeat = T;
		}
	}

	public static void SetDownbeat() {
		DownbeatPhase = BeatPhase;
		Tick();
	}

	public static float Interval(float beats) => T % beats / beats;

#region tap tempo

	static readonly TimeSpan TapResetTime = new(0, 0, seconds: 2);
	const int MinTaps = 4;

	static readonly Stopwatch tapTimer = Stopwatch.StartNew();
	static TimeSpan tappingTime = -TapResetTime;
	static int tapCounter = 0;

	public static Tempo TapTempo { get; private set; }

	static TimeSpan SinceLastTap => tapTimer.Elapsed - tappingTime;
	public static bool IsTapTempoStale => SinceLastTap >= TapResetTime;

	public static void Tap() {
		if (IsTapTempoStale) {
			tapCounter = 0;
			tappingTime = TimeSpan.Zero;
			tapTimer.Restart();
		} else {
			tappingTime = tapTimer.Elapsed;
		}
		tapCounter++;

		if (tapCounter >= MinTaps && !IsTapTempoStale) {
			var currentTempo = Tempo.FromBeatDuration(tappingTime / (tapCounter - 1));
			if (currentTempo.IsUsable) {
				TapTempo = currentTempo;
			}
		} else {
			TapTempo = Tempo.Zero;
		}
	}

	public static void ApplyTapTempo() {
		if (TapTempo) {
			tempo = TapTempo.Rounded();
		}
	}

#endregion
}
