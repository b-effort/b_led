using static ImGuiNET.ImGui;
using static b_effort.b_led.Widgets;
using static b_effort.b_led.ImGuiShorthand;

namespace b_effort.b_led;

sealed class PreviewWindow : IDisposable {
	const int Resolution = State.BufferWidth;

	readonly Image image;
	readonly Texture2D texture;

	public PreviewWindow() {
		this.image = rl.GenImageColor(Resolution, Resolution, rlColor.BLACK);
		this.texture = rl.LoadTextureFromImage(this.image);
	}

	~PreviewWindow() => this.Dispose();

	public void Dispose() {
		rl.UnloadImage(this.image);
		rl.UnloadTexture(this.texture);
		GC.SuppressFinalize(this);
	}

	public unsafe void Show() {
		var pixels = (rlColor*)this.image.data;
		var buffer = State.previewBuffer;
		for (var y = 0; y < Resolution; y++) {
			for (var x = 0; x < Resolution; x++) {
				pixels[y * Resolution + x] = buffer[y, x];
			}
		}
		rl.UpdateTexture(this.texture, pixels);

		SetNextWindowSize(em(24, 24), ImGuiCond.FirstUseEver);
		Begin("preview");
		{
			ImGuiUtil.ImageTextureFit(this.texture);
		}
		End();
	}
}

sealed class PalettesWindow : IDisposable {
	readonly GradientEditState editState = new();

	~PalettesWindow() => this.Dispose();

	public void Dispose() {
		this.editState.Dispose();
		GC.SuppressFinalize(this);
	}

	public void Show() {
		SetNextWindowSize(em(24, 12), ImGuiCond.FirstUseEver);
		Begin("palette");
		{
			var palette = State.Palette;
			if (palette != null)
				GradientEdit("selected_palette", palette.Gradient, this.editState);
		}
		End();
	}
}

sealed class MetronomeWindow {
	readonly float[] beatPoints = new float[144];
	int beatOffset = 0;

	public void Show() {
		SetNextWindowSize(em(24, 12), ImGuiCond.FirstUseEver);
		Begin("metronome");
		{
			if (Button("tap")) {
				Metronome.Tap();
			}

			SameLine();
			var bpmStr = Metronome.TapTempo.bpm.ToString("000.00");
			if (Metronome.TapTempo && !Metronome.IsTapTempoStale)
				Text(bpmStr);
			else
				TextDisabled(bpmStr);

			SameLine();
			if (Button("set")) {
				Metronome.ApplyTapTempo();
				Metronome.SetDownbeat();
			}

			SameLine();
			if (Button("downbeat")) {
				Metronome.SetDownbeat();
			}

			InputFloat("tempo", ref Metronome.tempo.bpm, 1f, 10f, "%.2f");

			var points = this.beatPoints;
			points[this.beatOffset] = Metronome.BeatPulse;
			this.beatOffset = points.NextOffset(this.beatOffset);
			PlotLines("beat", ref points[0], points.Length, this.beatOffset);
		}
		End();
	}
}

sealed class MacrosWindow {
	static Pattern? Pattern => State.Pattern;

	public void Show() {
		SetNextWindowSize(em(24, 12), ImGuiCond.FirstUseEver);
		Begin("macros");
		{
			if (BeginTable("macros_table", 2, ImGuiTableFlags.BordersInnerH)) {
				TableSetupColumn("global");
				TableSetupColumn("pattern");
				TableHeadersRow();
				TableNextRow();

				TableSetColumnIndex(0);
				foreach (var macro in Macro.Global) {
					MacroKnob(macro);
					SameLine();
				}

				TableSetColumnIndex(1);
				if (Pattern != null) {
					foreach (var macro in Pattern.Macros) {
						MacroKnob(macro);
						SameLine();
					}
				}
			}
			EndTable();
		}
		End();
	}

	static void MacroKnob(Macro macro) {
		var value = macro.Value;
		if (Knob(macro.Name, ref value, macro.Min, macro.Max, width: em(4), flags: KnobFlags.NoInput)) {
			macro.Value = value;
		}
	}
}

sealed class PushWindow {
	static readonly uint color_connected = hsb(120f / 360).ToU32();
	static readonly uint color_disconnected = hsb(0).ToU32();

	public void Show() {
		SetNextWindowSize(em(24, 12), ImGuiCond.FirstUseEver);
		Begin("push");
		{
			float radius = em(0.45f);
			DrawList.AddCircleFilled(
				GetCursorScreenPos() + vec2(radius, radius + Style.ItemSpacing.Y),
				radius,
				Push2.IsConnected ? color_connected : color_disconnected
			);
			Dummy(vec2(radius * 1.8f, radius * 2));

			SameLine();
			if (!Push2.IsConnected) {
				if (Button("connect")) {
					Push2.Connect();
				}
			} else {
				if (Button("disconnect")) {
					Push2.Disconnect();
				}
			}

			AlignTextToFramePadding();
			var encTempo = Push2.Encoders[Push2.Encoder.Tempo].Value;
			if (Knob("enc/tempo", ref encTempo)) { }
			SameLine();
			var encSwing = Push2.Encoders[Push2.Encoder.Swing].Value;
			if (Knob("enc/swing", ref encSwing)) { }
		}
		End();
	}
}

sealed class FuncPlotterWindow {
	const int Resolution = State.BufferWidth;
	public List<(string name, Func<float, float> f)> Funcs { get; init; } = new();
	readonly float[] points = new float[Resolution];

	bool animate;

	public void Show() {
		SetNextWindowSize(em(24, 12), ImGuiCond.FirstUseEver);
		Begin("f(x) plotter");
		{
			Checkbox("animate", ref this.animate);

			var points = this.points;
			foreach (var (name, f) in this.Funcs) {
				for (var i = 0; i < Resolution; i++) {
					var x = (float)i / Resolution;
					if (this.animate)
						x += PatternScript.t;
					points[i] = f(x);
				}
				PlotLines(name, ref points[0], points.Length);
			}
		}
		End();
	}
}
