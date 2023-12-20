using System.Runtime.CompilerServices;
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
		var buffer = State.outputBuffer;
		for (var y = 0; y < Resolution; y++) {
			for (var x = 0; x < Resolution; x++) {
				pixels[y * Resolution + x] = (rlColor)buffer[y, x];
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
		// todo: might still need
		GC.SuppressFinalize(this);
	}

	public unsafe void Show() {
		SetNextWindowSize(em(24, 12), ImGuiCond.FirstUseEver);
		Begin("palettes");
		{
			var currentPalette = State.CurrentPalette;
			if (currentPalette != null)
				GradientEdit("current_palette", currentPalette.Preview, this.editState);
			
			SeparatorText("all palettes");
			PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero); 
			if (BeginListBox("##palettes_list", GetContentRegionAvail())) {
				int barHeight = emEven(1.5f);
				float width = GetContentRegionAvail().X - Style.FramePadding.X * 2;
				Vector2 barSize = vec2(width, barHeight);

				var palettes = State.Palettes;
				for (var i = 0; i < palettes.Count; i++) {
					var palette = palettes[i];
					var isSelected = palette == currentPalette;

					if (isSelected) {
						PushStyleColor(ImGuiCol.Button, GetColorU32(ImGuiCol.ButtonActive));
					}
					if (ImageButton($"##palette_{i}", palette.Preview.TextureId, barSize)) {
						State.CurrentPalette = palette; 
					}
					if (isSelected) {
						PopStyleColor(1);
					}

					if (IsItemHovered()) {
						SetTooltip(palette.name);
					}

					if (BeginDragDropSource()) {
						SetDragDropPayload(DragDropType.Palette, new nint(&i), sizeof(int));
						Text(palette.name);
						Image(palette.Preview.TextureId, vec2(width, em(1)));
						EndDragDropSource();
					}
				}
				
				EndListBox();
			}
			PopStyleColor(1);
		}
		End();
	}
}

sealed class PatternsWindow {
	public unsafe void Show() {
		SetNextWindowSize(em(24, 12), ImGuiCond.FirstUseEver);
		Begin("patterns");
		{
			var drawList = GetWindowDrawList();
			
			var currentPattern = State.CurrentPattern;
			var patterns = State.Patterns;

			Vector2 avail = GetContentRegionAvail();
			float cellMargin = Style.FramePadding.X * 2;
			float minColWidth = em(4) + cellMargin;
			int numCols = (int)(avail.X / minColWidth);

			if (numCols < 1)
				return;
			
			int numRows = (int)MathF.Ceiling((float)patterns.Length / numCols);
			Vector2 patternSize = vec2(MathF.Floor(avail.X / numCols) - cellMargin);
			float rowHeight = patternSize.Y + cellMargin;
			
			if (BeginTable("patterns_table", numCols, 0, avail)) {
				PushStyleVar(ImGuiStyleVar.CellPadding, 0);
				
				for (int row = 0, i = 0; row < numRows; row++) {
					TableNextRow(0, rowHeight);
					PushID(row);
					for (int col = 0; col < numCols; col++, i++) {
						TableSetColumnIndex(col);
						PushID(i);

						if (i < patterns.Length) {
							var pattern = patterns[i];
							
							var origin = GetCursorScreenPos();
							drawList.AddImage(
								p_min: origin,
								p_max: origin + patternSize,
								user_texture_id: pattern.TextureId
							);
							drawList.AddRect(
								p_min: origin,
								p_max: origin + patternSize,
								col: GetColorU32(ImGuiCol.Border),
								rounding: 0,
								flags: ImDrawFlags.None,
								thickness: 3
							);

							InvisibleButton(string.Empty, patternSize);
							if (IsItemHovered()) {
								SetTooltip(pattern.name);
							}

							if (BeginDragDropSource()) {
								SetDragDropPayload(DragDropType.Pattern, new nint(&i), sizeof(int));
								Text(pattern.name);
								Image(pattern.TextureId, patternSize);
								EndDragDropSource();
							}
							
						}
						PopID();
					}
					PopID();
				}

				PopStyleVar(1);
				EndTable();
			}
		}
		End();
	}
}

sealed class ClipsWindow {
	public unsafe void Show() {
		SetNextWindowSize(em(24, 12), ImGuiCond.FirstUseEver);
		Begin("clips");
		{
			var drawList = GetWindowDrawList();
			
			var clipBank = State.CurrentClipBank;
			var clips = clipBank.clips;
			int numCols = clipBank.numCols;
			int numRows = clipBank.numRows;

			Vector2 avail = GetContentRegionAvail();
			float cellMargin = Style.FramePadding.X * 2;
			Vector2 clipSize = vec2(MathF.Floor(avail.X / numCols) - cellMargin);
			float rowHeight = clipSize.Y + cellMargin;
			
			if (BeginTable("clips_table", numCols, 0, avail)) {
				PushStyleVar(ImGuiStyleVar.CellPadding, 0);
				for (var row = 0; row < numRows; row++) {
					TableNextRow(0, rowHeight);
					PushID(row);
					for (var col = 0; col < numCols; col++) {
						TableSetColumnIndex(col);
						var id = row * numCols + col;
						PushID(id);
						var clip = clips[row, col];

						var origin = GetCursorScreenPos();
						if (clip.HasContents) {
							switch (clip.Type) {
								case ClipType.Pattern:
								{
									var pattern = clip.Pattern!;
									drawList.AddImage(pattern.TextureId, origin, origin + clipSize);
									break;
								}
								case ClipType.Palette:
								{
									drawList.AddImage(clip.Palette!.Preview.TextureId, origin, origin + clipSize);
									break;
								}
								default: throw new ArgumentOutOfRangeException();
							}
						}
						drawList.AddRect(
							p_min: origin,
							p_max: origin + clipSize,
							col: GetColorU32(ImGuiCol.Border),
							rounding: 0,
							flags: ImDrawFlags.None,
							thickness: 3
						);
						
						InvisibleButton(string.Empty, clipSize);
						
						if (BeginDragDropTarget()) {
							var payload = AcceptDragDropPayload(null);
							if (payload.NativePtr != (void*)0) {
								if (payload.IsDataType(DragDropType.Pattern)) {
									int patternIndex = *(int*)payload.Data;
									Pattern pattern = State.Patterns[patternIndex];
									clip.Pattern = pattern;
								} else if (payload.IsDataType(DragDropType.Palette)) {
									int paletteIndex = *(int*)payload.Data;
									Palette palette = State.Palettes[paletteIndex];
									clip.Palette = palette;
								}
							}
							EndDragDropTarget();
						}
					}
					PopID();
				}
				PopStyleVar(1);
				EndTable();
			}
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
	static Pattern? Pattern => State.CurrentPattern;

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
			var drawList = GetWindowDrawList();
			float radius = em(0.45f);
			drawList.AddCircleFilled(
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
