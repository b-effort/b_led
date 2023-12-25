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
	public static readonly Phase Zero = (Phase)0f;
	
	public readonly float value = value % 1;

	public static explicit operator Phase(float value) => new(value);
	public static implicit operator float(Phase @this) => @this.value;
}

static class Metronome {
	public static Tempo tempo = 128;
	public static float speed = 1f;
	
	static float tLast;
	static float tDownbeat;
	static Phase beatPhaseLast;

	public static float T { [Impl(Inline)] get; private set; }
	public static float TSynced { [Impl(Inline)] get; private set; }
	public static float TDelta { [Impl(Inline)] get; private set; }

	public static Phase BeatPhase => (Phase)(T - tDownbeat);
	public static bool IsOnBeat => BeatPhase < beatPhaseLast;
	public static float BeatPulse => IsOnBeat ? 1f : 0f;

	// happy little typos
	public static void Tickle(float deltaTime) {
		tLast = T;
		beatPhaseLast = BeatPhase;
		
		T += deltaTime * tempo.BeatsPerSecond * speed;
		if (setDownbeatNextTick) {
			tDownbeat = T;
			setDownbeatNextTick = false;
		}
		TSynced = T - tDownbeat;
		TDelta = T - tLast;
	}

	static bool setDownbeatNextTick = false;

	public static void SetDownbeat() => setDownbeatNextTick = true;

	public static float SyncedInterval(float beats) => TSynced % beats / beats;

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
