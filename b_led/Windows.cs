using static ImGuiNET.ImGui;
using static b_effort.b_led.Widgets;
using static b_effort.b_led.ImGuiShorthand;
using static b_effort.b_led.Interop.ImGuiInternal;

namespace b_effort.b_led;

sealed class PreviewWindow : IDisposable {
	const int Resolution = Greg.BufferWidth;

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
		var buffer = Greg.outputBuffer;
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

sealed class PalettesWindow {
	Palette? selectedPalette;
	readonly GradientEditState editState = new();

	public unsafe void Show() {
		SetNextWindowSize(em(24, 12), ImGuiCond.FirstUseEver);
		Begin("palettes");
		{
			if (this.selectedPalette != null)
				GradientEdit("selected_palette", this.selectedPalette.preview, this.editState);
			
			SeparatorText("all palettes");
			PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero); 
			if (BeginListBox("##palettes_list", GetContentRegionAvail())) {
				int barHeight = emEven(1.5f);
				float width = GetContentRegionAvail().X - Style.FramePadding.X * 2;
				Vector2 barSize = vec2(width, barHeight);

				var palettes = Greg.Palettes;
				for (var i = 0; i < palettes.Count; i++) {
					var palette = palettes[i];
					var isSelected = palette == this.selectedPalette;

					if (isSelected)
						PushStyleColor(ImGuiCol.Button, GetColorU32(ImGuiCol.ButtonActive));
					if (ImageButton($"##palette_{i}", palette.preview.TextureId, barSize))
						this.selectedPalette = palette;
					if (isSelected)
						PopStyleColor(1);

					if (IsItemHovered())
						SetTooltip(palette.name);

					if (BeginDragDropSource()) {
						SetDragDropPayload(DragDropType.Palette, new nint(&i), sizeof(int));
						Text(palette.name);
						Image(palette.preview.TextureId, vec2(width, em(1)));
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
	Pattern? selectedPattern;
	
	public unsafe void Show() {
		SetNextWindowSize(em(24, 12), ImGuiCond.FirstUseEver);
		Begin("patterns");
		{
			Vector2 avail = GetContentRegionAvail();
			float cellMargin = Style.FramePadding.X * 2;
			float minColWidth = em(4) + cellMargin;
			int numCols = (int)(avail.X / minColWidth);

			if (numCols < 1)
				return;
			
			var patterns = Greg.Patterns;
			int numRows = (int)MathF.Ceiling((float)patterns.Length / numCols);
			Vector2 patternSize = vec2(MathF.Floor(avail.X / numCols) - cellMargin);
			float rowHeight = patternSize.Y + cellMargin;
			
			if (BeginTable("patterns_table", numCols, 0, avail)) {
				PushStyleVar(ImGuiStyleVar.CellPadding, 0);
				
				for (int row = 0, i = 0; row < numRows; row++) {
					TableNextRow(0, rowHeight);
					PushID(row);
					for (int col = 0; col < numCols && i < patterns.Length; col++, i++) {
						TableSetColumnIndex(col);
						PushID(i);
						var pattern = patterns[i];
						var isSelected = pattern == this.selectedPattern;
							
						if (isSelected)
							PushStyleColor(ImGuiCol.Button, GetColorU32(ImGuiCol.ButtonActive));
						if (PatternButton(string.Empty, pattern, patternSize))
							this.selectedPattern = pattern;
						if (isSelected)
							PopStyleColor(1);
							
						if (BeginDragDropSource()) {
							SetDragDropPayload(DragDropType.Pattern, new nint(&i), sizeof(int));
							Text(pattern.name);
							Image(pattern.TextureId, patternSize);
							EndDragDropSource();
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

sealed class SequencesWindow {
	Sequence? selectedSequence;
	readonly SequenceEdit sequenceEdit = new("selected_sequence");

	public void Show() {
		var sequences = Greg.Sequences;
		if (this.selectedSequence is null && sequences.Count > 0) {
			this.selectedSequence = sequences.First();
		}
		
		SetNextWindowSize(em(24, 12), ImGuiCond.FirstUseEver);
		Begin("sequences");
		{
			var drawList = GetWindowDrawList();

			if (this.selectedSequence != null) {
				this.sequenceEdit.Draw(this.selectedSequence);
			}

			SeparatorText("all sequences");
			PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
			if (BeginListBox("##sequences_list", GetContentRegionAvail())) {
				for (var i = 0; i < sequences.Count; i++) {
					var sequence = sequences[i];
					var isSelected = sequence == this.selectedSequence;

					if (Selectable(sequence.name, isSelected))
						this.selectedSequence = sequence;
				}

				EndListBox();
			}
			PopStyleColor(1);
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
			
			var clipBank = Greg.ActiveClipBank;
			if (clipBank is null)
				return;

			var clips = clipBank.clips;
			const int numCols = ClipBank.NumCols;
			const int numRows = ClipBank.NumRows;

			Vector2 avail = GetContentRegionAvail();
			float cellMargin = Style.FramePadding.X * 2;
			Vector2 clipSize = vec2(MathF.Floor(avail.X / numCols) - cellMargin);
			float rowHeight = clipSize.Y + cellMargin;
			Vector2 clipBorder = vec2(2);

			if (BeginTable("clips_table", numCols, 0, avail)) {
				PushStyleVar(ImGuiStyleVar.CellPadding, 0);
				for (int row = 0, i = 0; row < numRows; row++) {
					TableNextRow(0, rowHeight);
					PushID(row);
					for (int col = 0; col < numCols; col++, i++) {
						TableSetColumnIndex(col);
						PushID(i);
						var clip = clips[row][col];
						var origin = GetCursorScreenPos();
						
						if (InvisibleButton(string.Empty, clipSize)) {
							clipBank.Activate(clip);
						}
						bool isActive = clipBank.IsActive(clip);
						bool isHovered = IsItemHovered();
						uint frameColor = GetColorU32(
							(isHovered && clip.HasContents, isActive) switch {
								(true, true)  => ImGuiCol.ButtonActive,
								(true, false) => ImGuiCol.ButtonHovered,
								_             => ImGuiCol.Border,
							}
						);
						RenderFrame(
							p_min: origin,
							p_max: origin + clipSize,
							frameColor
						);
						drawList.AddImageOrEmpty(
							clip.Contents?.TextureId,
							p_min: origin + clipBorder,
							p_max: origin + clipSize - clipBorder
						);
						
						if (BeginDragDropTarget()) {
							var payload = AcceptDragDropPayload(DragDropType.Palette);
							if (payload.NativePtr != (void*)0) {
								int patternIndex = *(int*)payload.Data;
								Pattern pattern = Greg.Patterns[patternIndex];
								clip.Contents = pattern;
							} else {
								payload = AcceptDragDropPayload(DragDropType.Pattern);
							}
							if (payload.NativePtr != (void*)0) {
								int paletteIndex = *(int*)payload.Data;
								Palette palette = Greg.Palettes[paletteIndex];
								clip.Contents = palette;
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
	static Pattern? Pattern => Greg.ActivePattern;

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
				EndTable();
			}
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

			SameLine();
			// AlignTextToFramePadding();
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
	const int Resolution = Greg.BufferWidth;
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
