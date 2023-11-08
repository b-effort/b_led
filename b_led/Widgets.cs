using System.Runtime.CompilerServices;
using IconFonts;
using static b_effort.b_led.ImGuiShorthand;
using static b_effort.b_led.Interop.ImGuiInternal;
using static ImGuiNET.ImGui;

namespace b_effort.b_led;

static class Widgets {
#region knob

	// https://github.com/altschuler/imgui-knobs/tree/main

	public enum KnobVariant {
		Tick,
		Dot,
		Wiper,
		WiperOnly,
		WiperDot,
		Stepped,
		Space,
	};

	[Flags] public enum KnobFlags {
		None = 0,
		ReadOnly = 1 << 0,
		NoTitle = 1 << 1,
		NoInput = 1 << 2,
		NoTooltip = 1 << 3,
		DragHorizontal = 1 << 4,
	};

	public static unsafe bool Knob(
		string label,
		ref float value,
		float min = 0f,
		float max = 1f,
		float speed = 0.01f,
		string format = "%.1f",
		KnobVariant variant = KnobVariant.Stepped,
		int ticks = 10,
		KnobFlags flags = KnobFlags.None,
		float width = 0
	) {
		const ImGuiDataType dataType = ImGuiDataType.Float;
		const float angleMin = MathF.PI * 0.75f;
		const float angleMax = MathF.PI * 2.25f;

		if (width == 0) {
			width = GetTextLineHeight() * 4f;
		}

		float radius = width / 2f;
		float value01 = (value - min) / (max - min);
		var angle = angleMin + (angleMax - angleMin) * value01;

		Vector2 knobOrigin;

		bool changed = false;

		PushID(label);
		PushItemWidth(width);
		BeginGroup();
		{
			uint uid = GetID(label);

			if ((flags & KnobFlags.NoTitle) == 0) {
				PushFont(ImFonts.Mono_15);
				var titleSize = CalcTextSize(label, false, width);
				var posX = GetCursorPosX() + (width - titleSize.X) / 2f;
				SetCursorPosX(posX);
				PushTextWrapPos(posX + titleSize.X);

				Text(label);

				PopTextWrapPos();
				PopFont();
			}

			knobOrigin = GetCursorScreenPos();
			InvisibleButton(label, vec2(width));

			ImGuiSliderFlags dragFlags = ImGuiSliderFlags.None;
			if ((flags & KnobFlags.DragHorizontal) == 0) {
				dragFlags |= ImGuiSliderFlags_Vertical;
			}
			if ((flags & KnobFlags.ReadOnly) == 0) {
				changed = DragBehavior(
					uid, dataType,
					(nint)Unsafe.AsPointer(ref value),
					speed,
					(nint)Unsafe.AsPointer(ref min), (nint)Unsafe.AsPointer(ref max),
					format, dragFlags
				);
			}

			if ((flags & KnobFlags.NoTooltip) == 0
			 && (IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) || IsItemActive())) {
				SetTooltip($"{value:00%}% - {label}");
			}

			if ((flags & KnobFlags.NoInput) == 0) {
				if (DragScalar(
					    "###knob_drag", dataType,
					    (nint)Unsafe.AsPointer(ref value),
					    speed,
					    (nint)Unsafe.AsPointer(ref min), (nint)Unsafe.AsPointer(ref max),
					    format, dragFlags
				    )) {
					changed = true;
				}
			}
		}
		EndGroup();
		PopItemWidth();
		PopID();

		var center = knobOrigin + vec2(radius);
		var drawList = GetWindowDrawList();

		switch (variant) {
			case KnobVariant.Tick:      throw new NotImplementedException();
			case KnobVariant.Dot:       throw new NotImplementedException();
			case KnobVariant.Wiper:     throw new NotImplementedException();
			case KnobVariant.WiperOnly: throw new NotImplementedException();
			case KnobVariant.WiperDot:  throw new NotImplementedException();
			case KnobVariant.Stepped:
			{
				for (var i = 0; i < ticks; i++) {
					var stepAngle = angleMin + (angleMax - angleMin) * (i / (ticks - 1f));
					DrawTick(0.7f, 0.9f, 0.04f, stepAngle, hsb(320 / 360f, 1, 1));
				}
				DrawCircle(0.6f, new HSB(0, 1, 0.3f), 32);
				DrawDot(0.12f, 0.4f, new HSB(320 / 360f, 1, 1), 12);

				break;
			}
			case KnobVariant.Space: throw new NotImplementedException();
			default:                throw new ArgumentOutOfRangeException(nameof(variant), variant, null);
		}

		return changed;

		void DrawCircle(float radiusScale, HSB color, int numSegments) {
			drawList.AddCircleFilled(center, radius * radiusScale, color.ToU32(), numSegments);
		}

		void DrawDot(float size, float radiusScale, HSB color, int numSegments) {
			var dotRadius = radiusScale * radius;

			drawList.AddCircleFilled(
				center + vec2(MathF.Cos(angle) * dotRadius, MathF.Sin(angle) * dotRadius),
				size * radius,
				color.ToU32(),
				numSegments
			);
		}

		void DrawTick(float start, float end, float thickness, float tickAngle, HSB color) {
			var tickStart = start * radius;
			var tickEnd = end * radius;
			var sin = MathF.Sin(tickAngle);
			var cos = MathF.Cos(tickAngle);

			drawList.AddLine(
				center + vec2(cos * tickEnd, sin * tickEnd),
				center + vec2(cos * tickStart, sin * tickStart),
				color.ToU32(),
				thickness * radius
			);
		}
	}

#endregion

#region gradient

	public sealed class GradientEditState : IDisposable {
		const int Resolution = 32;

		Gradient? gradient;
		public Gradient? Gradient {
			get => this.gradient;
			set {
				var needsUpdate = value != this.gradient && value != null;
				this.gradient = value;
				if (needsUpdate) {
					this.UpdateTexture();
				}
			}
		}

		public int SelectedIndex { get; private set; } = -1;
		public HSB RevertColor { get; private set; } = new();
		public bool IsDragging { get; set; } = false;

		public readonly Texture2D texture;
		readonly rlColor[] pixels;

		public GradientEditState() {
			this.texture = RaylibUtil.CreateTexture(Resolution, 1, out this.pixels);
		}

		~GradientEditState() => this.Dispose();

		public void Dispose() {
			this.gradient = null;
			rl.UnloadTexture(this.texture);
			GC.SuppressFinalize(this);
		}

		public void SelectPoint(int i) {
			var points = this.gradient!.Points;
			int len = points.Count;

			if (i < 0)
				i += len;
			else if (i >= len)
				i %= len;

			this.SelectedIndex = i;
			this.RevertColor = points[i].color;
		}

		public void UpdateTexture() {
			var pixels = this.pixels;
			for (var x = 0; x < Resolution; x++) {
				var color = this.gradient!.MapColor(hsb((float)x / Resolution));
				pixels[x] = color.ToRGB();
			}
			rl.UpdateTexture(this.texture, pixels);
		}
	}

	public static bool GradientEdit(string id, Gradient gradient, GradientEditState state) {
		state.Gradient = gradient;
		if (state.SelectedIndex < 0) {
			state.SelectPoint(0);
		}
		
		int barHeight = emEven(1);
		float marginX = emEven(0.2f);
		float markerWidth = emOdd(0.6f);
		float markerHeight = emEven(1f);
		Vector2 markerSize = vec2(markerWidth, markerHeight);

		bool changed = false;

		var io = GetIO();
		var drawList = GetWindowDrawList();

		PushID(id);
		BeginGroup();
		{
			float width = GetContentRegionAvail().X - marginX * 2;
			Vector2 origin = GetCursorScreenPos() + vec2(marginX, 0);

			{ // # color bar
				Vector2 barSize = vec2(width, barHeight);

				SetCursorScreenPos(origin);
				Image((nint)state.texture.id, barSize);
			}


			bool isMarkerHovered = false;
			{ // # markers
				origin.Y = GetCursorScreenPos().Y;
				bool isMouseLeftDown = IsMouseDown(0);
				bool isMouseDragging = IsMouseDragging(0);

				var points = gradient.Points;
				for (var i = 0; i < points.Count; i++) {
					var point = points[i];
					float x = point.pos * width;
					bool isSelected = i == state.SelectedIndex;

					DrawMarker(origin, x, point.color, isSelected);

					SetCursorScreenPos(origin + vec2(x - markerWidth / 2, 0));
					InvisibleButton($"##marker_{i}", markerSize);

					isMarkerHovered |= IsItemHovered();

					if (!state.IsDragging && isMarkerHovered && isMouseLeftDown) {
						state.SelectPoint(i);
						state.IsDragging = true;
					}

					if (!isMouseLeftDown) {
						state.IsDragging = false;
					}

					if (
						i == state.SelectedIndex
					 && i != 0 && i < points.Count - 1 // no dragging endpoints
					 && state.IsDragging && isMouseDragging
					) {
						float deltaPos = io.MouseDelta.X / width;
						point.pos = BMath.clamp(point.pos + deltaPos);
						changed |= deltaPos != 0;
					}
				}

				if (changed) {
					gradient.Sort();
				}
			}

			{ // # markers area
				SetCursorScreenPos(origin);
				InvisibleButton("markers_area", vec2(width, markerHeight));

				if (!isMarkerHovered && IsItemHovered()) {
					float x = io.MousePos.X - origin.X;
					float gradientPos = x / width;
					HSB color = gradient.ColorAt(gradientPos);
					DrawMarker(origin, x, color, isSelected: false, showOutline: false);

					if (IsMouseClicked(0)) {
						int i = gradient.Add(gradientPos, color);
						state.SelectPoint(i);
						changed = true;
					}
				}
			}

			SpacingY(em(0.25f));

			var selectedPoint = gradient.Points[state.SelectedIndex];

			{ // # controls
				if (ArrowButton("##prev", ImGuiDir.Left)) {
					state.SelectPoint(state.SelectedIndex - 1);
				}
				SameLine();
				if (ArrowButton("##next", ImGuiDir.Right)) {
					state.SelectPoint(state.SelectedIndex + 1);
				}

				const ImGuiColorEditFlags colorButtonFlags = ImGuiColorEditFlags.NoAlpha
				                                           | ImGuiColorEditFlags.InputHSV;
				SameLine();
				ColorButton("##edit_current", selectedPoint.color, colorButtonFlags);

				SameLine();
				RGB revertRgb = state.RevertColor.ToRGB();
				uint revertButtonColor = revertRgb.ToU32();
				PushStyleColor(ImGuiCol.Button, revertButtonColor);
				PushStyleColor(ImGuiCol.ButtonHovered, revertButtonColor);
				PushStyleColor(ImGuiCol.ButtonActive, revertButtonColor);
				PushStyleColor(ImGuiCol.Text, revertRgb.ContrastColor().ToU32());
				if (Button("revert##edit_revert")) {
					selectedPoint.color = state.RevertColor;
				}
				SameLine();
				if (Button(FontAwesome6.Trash)) {
					
				}
				PopStyleColor(4);
			}

			SpacingY(em(0.25f));

			{ // # edit color picker
				if (ColorPickerHSB("##hsb_picker", ref selectedPoint.color)) {
					changed = true;
				}
			}
		}
		EndGroup();
		PopID();

		if (changed) {
			state.UpdateTexture();
		}

		return changed;

		void DrawMarker(Vector2 origin, float x, HSB color, bool isSelected, bool showOutline = true) {
			float marginOutline = emEven(0.1f);
			float halfH = markerHeight / 2;
			float halfW = markerWidth / 2;

			var min = origin + vec2(x - halfW, 0);

			if (showOutline) {
				HSB outlineColor = isSelected ? hsb(320 / 360f) : hsb(0, 0, 0.2f);
				uint imOutlineColor = outlineColor.ToU32();
				drawList.AddTriangleFilled(
					min + vec2(halfW, 0),
					min + vec2(0, halfH),
					min + vec2(markerWidth, halfH),
					imOutlineColor
				);
				drawList.AddRectFilled(
					min + vec2(0, halfH),
					min + markerSize,
					imOutlineColor
				);
			}

			uint imColor = color.ToU32();
			drawList.AddTriangleFilled(
				min + vec2(halfW, marginOutline * 1.5f),
				min + vec2(marginOutline, halfH),
				min + vec2(markerWidth - marginOutline, halfH),
				imColor
			);
			drawList.AddRectFilled(
				min + vec2(marginOutline, halfH),
				min + markerSize - vec2(marginOutline),
				imColor
			);
		}
	}

	static readonly uint[] RainbowHues = {
		new RGB(255, 000, 000).ToU32(),
		new RGB(255, 255, 000).ToU32(),
		new RGB(000, 255, 000).ToU32(),
		new RGB(000, 255, 255).ToU32(),
		new RGB(000, 000, 255).ToU32(),
		new RGB(255, 000, 255).ToU32(),
		new RGB(255, 000, 000).ToU32(),
	};

	public static bool ColorPickerHSB(string id, ref HSB color) {
		bool changed = false;

		var io = GetIO();
		var drawList = GetWindowDrawList();
		var style = GetStyle();

		PushID(id);
		BeginGroup();
		{
			float dragWidth = em(4);
			float barMarginX = em(0.2f);
			Vector2 barSize = vec2(
				x: GetContentRegionAvail().X - dragWidth - barMarginX - style.ItemSpacing.X,
				y: emEven(1.25f)
			);

			float arrowsHalfSizeVal = MathF.Floor(barSize.Y * 0.2f);
			Vector2 arrowHalfSize = vec2(arrowsHalfSizeVal, arrowsHalfSizeVal + 1);

			Vector2 barOrigin;

			{ // # hue
				DragComponent("h", ref color.h);

				SameLine();
				barOrigin = GetCursorScreenPos();

				InvisibleButton("##hue", barSize);
				if (IsItemActive()) {
					color.h = BMath.clamp((io.MousePos.X - barOrigin.X) / barSize.X);
					changed = true;
				}

				for (var i = 0; i < 6; i++) {
					float partWidth = barSize.X / 6;
					uint col1 = RainbowHues[i];
					uint col2 = RainbowHues[i + 1];
					drawList.AddRectFilledMultiColor(
						p_min: barOrigin + vec2(i * partWidth, 0),
						p_max: barOrigin + vec2((i + 1) * partWidth, barSize.Y),
						col_upr_left: col1, col_bot_left: col1,
						col_upr_right: col2, col_bot_right: col2
					);
				}

				RenderFrameBorder(barOrigin, barOrigin + barSize, 0);
				DrawArrows(barOrigin + vec2(color.h * barSize.X, 0));
			}

			{ // # saturation
				DragComponent("s", ref color.s);

				SameLine();
				barOrigin = GetCursorScreenPos();
				var barMax = barOrigin + barSize;

				InvisibleButton("##saturation", barSize);
				if (IsItemActive()) {
					color.s = BMath.clamp((io.MousePos.X - barOrigin.X) / barSize.X);
					changed = true;
				}

				uint col1 = (color with { s = 0, b = 1 }).ToU32();
				uint col2 = (color with { s = 1, b = 1 }).ToU32();
				drawList.AddRectFilledMultiColor(
					p_min: barOrigin, p_max: barMax,
					col_upr_left: col1, col_bot_left: col1,
					col_upr_right: col2, col_bot_right: col2
				);

				RenderFrameBorder(barOrigin, barMax, 0);
				DrawArrows(barOrigin + vec2(color.s * barSize.X, 0));
			}

			{ // # brightness
				DragComponent("b", ref color.b);

				SameLine();
				barOrigin = GetCursorScreenPos();
				var barMax = barOrigin + barSize;

				InvisibleButton("##brightness", barSize);
				if (IsItemActive()) {
					color.b = BMath.clamp((io.MousePos.X - barOrigin.X) / barSize.X);
					changed = true;
				}

				uint col1 = (color with { b = 0 }).ToU32();
				uint col2 = (color with { b = 1 }).ToU32();
				drawList.AddRectFilledMultiColor(
					p_min: barOrigin, p_max: barMax,
					col_upr_left: col1, col_bot_left: col1,
					col_upr_right: col2, col_bot_right: col2
				);

				RenderFrameBorder(barOrigin, barMax, 0);
				DrawArrows(barOrigin + vec2(color.b * barSize.X, 0));
			}

			void DrawArrows(Vector2 arrowPos) => RenderArrowsForHorizontalBar(
				drawList, arrowPos, arrowHalfSize, barSize.Y, style.Alpha
			);

			void DragComponent(string name, ref float value) {
				PushItemWidth(dragWidth);
				DragFloat($"##{name}_drag", ref value, 0.01f, 0, 1, $"{name}: %.3f", ImGuiSliderFlags.AlwaysClamp);
				PopItemWidth();
			}
		}
		EndGroup();
		PopID();

		return changed;
	}

	static void RenderArrowsForHorizontalBar(
		ImDrawListPtr drawList,
		Vector2 pos,
		Vector2 halfSize,
		float barHeight,
		float alpha
	) {
		byte alpha8 = f32_to_int8(alpha);
		uint colorArrow = new RGB(255, 255, 255, alpha8).ToU32();
		uint colorOutline = new RGB(0, 0, 0, alpha8).ToU32();
		var halfSizeOutline = halfSize + vec2(1, 2);

		RenderArrowPointingAt(drawList, pos + vec2(0, halfSize.Y), halfSizeOutline, ImGuiDir.Down, colorOutline);
		RenderArrowPointingAt(drawList, pos + vec2(0, halfSize.Y - 1), halfSize, ImGuiDir.Down, colorArrow);

		RenderArrowPointingAt(
			drawList, pos + vec2(0, barHeight - halfSize.Y), halfSizeOutline, ImGuiDir.Up, colorOutline
		);
		RenderArrowPointingAt(drawList, pos + vec2(0, barHeight - halfSize.Y + 1), halfSize, ImGuiDir.Up, colorArrow);
	}

#endregion
}
