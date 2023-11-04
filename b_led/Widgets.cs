using System.Runtime.CompilerServices;
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
		const int Resolution = 128;

		public int selectedIndex = -1;
		public bool isDragging = false;
		public Vector4 revertColor = new();

		public readonly Texture2D texture;
		public bool isTextureStale = true;
		readonly rlColor[] pixels;

		public GradientEditState() {
			this.texture = RaylibUtil.CreateTexture(Resolution, 1, out this.pixels);
		}

		~GradientEditState() => this.Dispose();

		public void Dispose() {
			rl.UnloadTexture(this.texture);
			GC.SuppressFinalize(this);
		}

		public void SelectPoint(Gradient gradient, int i) {
			var points = gradient.Points;
			int len = points.Count;

			if (i < 0)
				i += len;
			else if (i >= len)
				i %= len;

			this.selectedIndex = i;
			this.revertColor = points[i].color;
		}

		public void UpdateTexture(Gradient gradient) {
			var pixels = this.pixels;
			for (var x = 0; x < Resolution; x++) {
				var color = gradient.MapColor(hsb((float)x / Resolution));
				pixels[x] = color.ToRGB();
			}
			rl.UpdateTexture(this.texture, pixels);
			this.isTextureStale = false;
		}
	}

	public static bool GradientEdit(string id, Gradient gradient, GradientEditState state) {
		int barHeight = emEven(1);
		float markerWidth = emOdd(0.6f);
		float markerHeight = emEven(1f);
		Vector2 markerSize = vec2(markerWidth, markerHeight);

		bool changed = false;

		var io = GetIO();
		var drawList = GetWindowDrawList();

		if (state.selectedIndex < 0) {
			state.SelectPoint(gradient, 0);
		}
		if (state.isTextureStale) {
			state.UpdateTexture(gradient);
		}

		PushID(id);
		{
			float marginX = emEven(0.2f);
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
					bool isSelected = i == state.selectedIndex;

					DrawMarker(origin, x, point.color, isSelected);

					SetCursorScreenPos(origin + vec2(x - markerWidth / 2, 0));
					InvisibleButton($"##marker_{i}", markerSize);

					isMarkerHovered |= IsItemHovered();

					if (!state.isDragging && isMarkerHovered && isMouseLeftDown) {
						state.SelectPoint(gradient, i);
						state.isDragging = true;
					}

					if (!isMouseLeftDown) {
						state.isDragging = false;
					}

					if (
						i == state.selectedIndex
					 && i != 0 && i < points.Count - 1 // no dragging endpoints
					 && state.isDragging && isMouseDragging
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
						gradient.Add(gradientPos, color);
						changed = true;
					}
				}
			}

			SpacingY(em(0.25f));

			var selectedPoint = gradient.Points[state.selectedIndex];
			Vector4 selectedColorVec = selectedPoint.color;

			{ // # controls
				if (ArrowButton("##prev", ImGuiDir.Left)) {
					state.SelectPoint(gradient, state.selectedIndex - 1);
				}
				SameLine();
				if (ArrowButton("##next", ImGuiDir.Right)) {
					state.SelectPoint(gradient, state.selectedIndex + 1);
				}

				const ImGuiColorEditFlags colorButtonFlags = ImGuiColorEditFlags.NoAlpha
				                                           | ImGuiColorEditFlags.InputHSV;
				SameLine();
				ColorButton("##edit_current", selectedColorVec, colorButtonFlags);
				
				SameLine();
				uint revertButtonColor = ((HSB)state.revertColor).ToU32();
				PushStyleColor(ImGuiCol.Button, revertButtonColor);
				PushStyleColor(ImGuiCol.ButtonHovered, revertButtonColor);
				PushStyleColor(ImGuiCol.ButtonActive, revertButtonColor);
				if (Button("revert##edit_revert")) {
					selectedColorVec = selectedPoint.color = state.revertColor;
				}
				PopStyleColor(3);
			}

			SpacingY(em(0.25f));

			{ // # edit color picker

				if (ColorPicker4(
					    "##edit_picker", ref selectedColorVec, ImGuiColorEditFlags.DisplayHSV, ref state.revertColor.X
				    )) {
					selectedPoint.color = selectedColorVec;
					changed = true;
				}
			}
		}
		PopID();

		if (changed) {
			state.isTextureStale = true;
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

#endregion
}
