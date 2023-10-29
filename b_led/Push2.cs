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
	static readonly byte[] SysExPrefaceBytes = { 0x00, 0x21, 0x1D, 0x01, 0x01 };

	static IMidiInputDevice? input;
	static IMidiOutputDevice? output;

	[MemberNotNullWhen(true, nameof(input), nameof(output))]
	public static bool IsConnected => input?.IsOpen == true && output?.IsOpen == true;

	public static void Connect() {
		input ??= MidiDeviceManager.Default.InputDevices.First(d => d.Name.TrimEnd() == MidiDeviceName)
			.CreateDevice();
		output ??= MidiDeviceManager.Default.OutputDevices.First(d => d.Name.TrimEnd() == MidiDeviceName)
			.CreateDevice();

		input.Open();
		output.Open();

		SetMidiMode(MidiMode.Dual);

		// default palette
		SetPalletEntry(0, hsb(0, 0, 0), 0);
		for (var i = 1; i < PaletteSize; i++) {
			SetPalletEntry(i, hsb(1f / PaletteSize, 1, 1), (float)i / PaletteSize);
		}
		ReapplyPalette();
	}

	public static void Disconnect() {
		input?.Close();
		output?.Close();
	}

	public static void Dispose() {
		input?.Dispose();
		input = null;
		output?.Dispose();
		output = null;
	}

	// ! y starts from the top. it gets inverted to start from the bottom like the push does
	public readonly record struct Pad(int x, int y) {
		readonly int x = x % 8;
		readonly int y = y % 8;

		public Key GetKey() => (Key)(36 + this.x + (7 - this.y) * 8);
	}

	public static void SendPad(Pad pad, int brightness) {
		if (!IsConnected)
			return;

		if (brightness > 0) {
			output.Send(new NoteOnMessage(MidiChannel, pad.GetKey(), brightness));
		} else {
			output.Send(new NoteOffMessage(MidiChannel, pad.GetKey(), brightness));
		}
	}

	public enum Control {
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

	public static void SendCC() { }

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

#region sysex

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
