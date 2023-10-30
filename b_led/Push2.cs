using System.Diagnostics.CodeAnalysis;
using System.Linq;
using RtMidi.Core;
using RtMidi.Core.Devices;
using RtMidi.Core.Enums;
using RtMidi.Core.Messages;

namespace b_effort.b_led;

static class Push2 {
	const string MidiDeviceName = "Ableton Push 2";
	const Channel MidiChannel = Channel.Channel1;
	const int PaletteSize = 128;

	static MidiDeviceManager DeviceManager => MidiDeviceManager.Default;

	static IMidiInputDevice? input;
	static IMidiOutputDevice? output;

	[MemberNotNullWhen(true, nameof(input), nameof(output))]
	public static bool IsConnected => input?.IsOpen == true && output?.IsOpen == true;

	public static void Connect() {
		input ??= DeviceManager.InputDevices.FirstOrDefault(d => d.Name.TrimEnd() == MidiDeviceName)
			?.CreateDevice();
		output ??= DeviceManager.OutputDevices.FirstOrDefault(d => d.Name.TrimEnd() == MidiDeviceName)
			?.CreateDevice();

		if (input is null || output is null) {
			Console.WriteLine("Push 2 not found");
			return;
		}

		input.Open();
		output.Open();

		input.NoteOn += OnNoteOn;
		input.NoteOff += OnNoteOff;
		input.ControlChange += OnControlChange;

		SetMidiMode(MidiMode.Dual);

		// default palette
		SetPalletEntry(0, hsb(0, 0, 0), 0);
		for (var i = 1; i < PaletteSize; i++) {
			SetPalletEntry(i, hsb(1f / PaletteSize, 1, 1), (float)i / PaletteSize);
		}
		ReapplyPalette();
	}

	public static void Disconnect() {
		if (input != null) {
			input.NoteOn -= OnNoteOn;
			input.NoteOff -= OnNoteOff;
			input.ControlChange -= OnControlChange;

			input.Close();
		}

		output?.Close();
	}

	public static void Dispose() {
		Disconnect();
		input?.Dispose();
		input = null;
		output?.Dispose();
		output = null;
	}

	public static void UpdateLEDs() {
		if (!IsConnected)
			return;

		UpdatePadLEDs();
		UpdateButtonLEDs();
	}

#region inputs

	readonly record struct NoteMessage(int note, Velocity velocity) {
		public static implicit operator NoteMessage(NoteOnMessage msg) => new((int)msg.Key, msg.Velocity);
		public static implicit operator NoteMessage(NoteOffMessage msg) => new((int)msg.Key, msg.Velocity);
	}

	static readonly Queue<NoteMessage> bufferedNoteChanges = new(8);
	static readonly Queue<ControlChangeMessage> bufferedControlChanges = new(8);


	public static void UpdateInputs() {
		foreach (var state in buttonsInputs.Values) {
			state.Tick();
		}

		while (bufferedNoteChanges.TryDequeue(out var msg)) {
			if (IsNotePad(msg.note)) {
				padsInputs[msg.note] = msg.velocity;
			}
		}

		while (bufferedControlChanges.TryDequeue(out var msg)) {
			var button = (Button)msg.Control;
			if (Enum.IsDefined(button)) {
				GetButton(button).Update(msg.Value > 0);
			}
		}
	}


	static void OnNoteOn(IMidiInputDevice sender, in NoteOnMessage msg) {
		if (msg.Channel == MidiChannel)
			bufferedNoteChanges.Enqueue(msg);
	}


	static void OnNoteOff(IMidiInputDevice sender, in NoteOffMessage msg) {
		if (msg.Channel == MidiChannel)
			bufferedNoteChanges.Enqueue(msg);
	}

	static void OnControlChange(IMidiInputDevice sender, in ControlChangeMessage msg) {
		if (msg.Channel == MidiChannel)
			bufferedControlChanges.Enqueue(msg);
	}

#endregion

#region pads

	// ! y starts from the top
	public readonly record struct Pad(int x, int y) {
		readonly int x = x % 8;
		readonly int y = y % 8;

		public int Index => this.x + (7 - this.y) * 8;
	}

	const int NumPads = 64;
	const int PadsFirstNote = 36;
	const int PadsLastNote = PadsFirstNote + NumPads - 1;
	static readonly Velocity[] padsInputs = new Velocity[NumPads];
	static readonly Velocity[] padsOutputs = new Velocity[NumPads];
	static readonly Velocity[] padsLastOutputs = new Velocity[NumPads];

	static bool IsNotePad(int note) => note is >= PadsFirstNote and <= PadsLastNote;

	// TODO: treat as hue
	public static void SetPadLED(Pad pad, float brightness) {
		padsOutputs[pad.Index] = Velocity.From01(brightness);
	}

	static void UpdatePadLEDs() {
		var outputs = padsOutputs;
		var outputsLast = padsLastOutputs;
		for (var i = 0; i < outputs.Length; i++) {
			int velocity = outputs[i];
			if (velocity == outputsLast[i])
				continue;

			var key = (Key)(PadsFirstNote + i);
			if (velocity > 0) {
				output!.Send(new NoteOnMessage(MidiChannel, key, velocity));
			} else {
				output!.Send(new NoteOffMessage(MidiChannel, key, velocity));
			}
			outputsLast[i] = velocity;
		}
	}

#endregion

#region buttons

	public enum Button {
		// left side
		TapTempo = 3,
		Metronome = 9,

		Delete = 118,
		Undo = 119,

		Mute = 60,
		Solo = 61,
		Stop = 29,

		Convert = 35,
		DoubleLoop = 117,
		Quantize = 116,

		Duplicate = 88,
		New = 87,

		FixedLength = 90,
		Automate = 89,
		Record = 86,

		Play = 85,

		// right side
		Setup = 30,
		User = 59,

		Device = 110,
		Browse = 111,
		Mix = 112,
		Clip = 113,

		AddDevice = 52,
		AddTrack = 53,

		Left = 44,
		Right = 45,
		Up = 46,
		Down = 47,

		Repeat = 56,
		Accent = 57,

		Scale = 58,
		Layout = 31,
		Note = 50,
		Session = 51,

		OctaveDown = 54,
		OctaveUp = 55,
		PageLeft = 62,
		PageRight = 63,

		Shift = 49,
		Select = 48,

		// track
		Track_1 = 20,
		Track_2 = 21,
		Track_3 = 22,
		Track_4 = 23,
		Track_5 = 24,
		Track_6 = 25,
		Track_7 = 26,
		Track_8 = 27,
		Track_Master = 28,

		// device
		Device_1 = 102,
		Device_2 = 103,
		Device_3 = 104,
		Device_4 = 105,
		Device_5 = 106,
		Device_6 = 107,
		Device_7 = 108,
		Device_8 = 109,

		// time div
		Time_4 = 36,
		Time_4t = 37,
		Time_8 = 38,
		Time_8t = 39,
		Time_16 = 40,
		Time_16t = 41,
		Time_32 = 42,
		Time_32t = 43,
	}

	public sealed class ButtonState {
		public bool IsPressed { get; private set; }
		// ReSharper disable once MemberHidesStaticFromOuterClass
		public bool WasPressed { get; private set; }

		public void Update(bool value) {
			this.WasPressed = value && !this.IsPressed;
			this.IsPressed = value;
		}

		public void Tick() => this.WasPressed = false;
	}

	static readonly Dictionary<Button, ButtonState> buttonsInputs = new();
	static readonly Dictionary<Button, Velocity> buttonsOutputs = new();
	static readonly Dictionary<Button, Velocity> buttonsLastOutputs = new();

	public static ButtonState GetButton(Button button) {
		if (buttonsInputs.TryGetValue(button, out var state)) {
			return state;
		}
		return buttonsInputs[button] = new ButtonState();
	}

	public static bool WasPressed(Button button) => GetButton(button).WasPressed;

	public static void SetButtonLED(Button button, float brightness) {
		buttonsOutputs[button] = Velocity.From01(brightness);
	}

	static void UpdateButtonLEDs() {
		var outputs = buttonsOutputs;
		var outputsLast = buttonsLastOutputs;
		foreach ((Button button, int velocity) in outputs) {
			if (velocity == outputsLast.GetValueOrDefault(button))
				continue;

			output!.Send(new ControlChangeMessage(MidiChannel, (int)button, velocity));
			outputsLast[button] = velocity;
		}
	}

#endregion

#region encoders

	public enum Encoder {
		Tempo = 14,
		Swing = 15,

		Track_1 = 71,
		Track_2 = 72,
		Track_3 = 73,
		Track_4 = 74,
		Track_5 = 75,
		Track_6 = 76,
		Track_7 = 77,
		Track_8 = 78,

		Master = 79,
	}

#endregion

#region sysex

	static readonly byte[] SysExPrefaceBytes = { 0x00, 0x21, 0x1D, 0x01, 0x01 };

	enum MidiMode {
		Live = 0,
		User = 1,
		Dual = 2,
	}

	enum SysExCommands : byte {
		SetMidiMode = 0x0a,
		SetPaletteEntry = 0x03,
		ReapplyPalette = 0x05,
	}

	static void SetMidiMode(MidiMode mode) => SendSysEx((byte)SysExCommands.SetMidiMode, (byte)mode);

	static void SetPalletEntry(int i, HSB hsb, float white) {
		var (r, g, b, w) = hsb.ToRGB(a: white);
		SendSysEx(
			(byte)SysExCommands.SetPaletteEntry,
			(byte)i,
			(byte)(r % 128), (byte)(r / 128),
			(byte)(g % 128), (byte)(g / 128),
			(byte)(b % 128), (byte)(b / 128),
			(byte)(w % 128), (byte)(w / 128)
		);
	}

	static void ReapplyPalette() => SendSysEx((byte)SysExCommands.ReapplyPalette);

	static void SendSysEx(params byte[] bytes) {
		if (!IsConnected)
			return;

		var data = SysExPrefaceBytes
			.Concat(bytes)
			.ToArray();
		output.Send(new SysExMessage(data));
	}

#endregion
}

readonly record struct Velocity {
	public readonly int value;

	public Velocity(int value) {
		if (value is < 0 or > 127)
			throw new ArgumentOutOfRangeException(nameof(value));

		this.value = value;
	}

	public static Velocity From01(float value) => new((int)MathF.Round(value * sbyte.MaxValue));

	public static implicit operator Velocity(int value) => new(value);
	public static implicit operator int(Velocity @this) => @this.value;
}
