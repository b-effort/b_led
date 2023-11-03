using System.Runtime.CompilerServices;
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

		public int? selectedIndex = null;
		public int? draggingIndex = null;

		public readonly Texture2D texture;
		readonly rlColor[] pixels;
		public bool isTextureStale = true;


		public GradientEditState() {
			this.texture = RaylibUtil.CreateTexture(Resolution, 1, out this.pixels);
		}

		~GradientEditState() => this.Dispose();

		public void Dispose() {
			rl.UnloadTexture(this.texture);
			GC.SuppressFinalize(this);
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
		const int BarHeight = 20;
		const float MarkerWidth = 10f;
		const float MarkerHeight = 15f;
		Vector2 markerSize = vec2(MarkerWidth, MarkerHeight);

		bool changed = false;

		var io = GetIO();
		var drawList = GetWindowDrawList();

		PushID(id);
		{
			var a = GetCursorScreenPos();
			float width = GetContentRegionAvail().X;

			{ // # color bar
				Vector2 barSize = vec2(width, BarHeight);

				if (state.isTextureStale) {
					state.UpdateTexture(gradient);
				}
				Image((nint)state.texture.id, barSize);
			}

			Vector2 markersOrigin = GetCursorScreenPos();
			bool isMarkerHovered = false;
			{ // # markers

				var points = gradient.Points;
				for (var i = 0; i < points.Count; i++) {
					var point = points[i];
					float x = point.pos * width;
					bool isSelected = i == state.selectedIndex;
					DrawMarker(markersOrigin, x, point.color, isSelected);

					SetCursorScreenPos(markersOrigin + vec2(x - MarkerWidth / 2, 0));
					InvisibleButton($"##marker_{i}", markerSize);
					isMarkerHovered |= IsItemHovered();

					bool isLeftMouseDown = IsMouseDown(0);
					if (state.draggingIndex is null && isMarkerHovered && isLeftMouseDown) {
						state.selectedIndex = state.draggingIndex = i;
					}

					if (!isLeftMouseDown) {
						state.draggingIndex = null;
					}

					if (i != 0 && i < points.Count - 1 && i == state.draggingIndex && IsMouseDragging(0)) {
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
				SetCursorScreenPos(markersOrigin);
				InvisibleButton("markers_area", vec2(width, MarkerHeight));

				if (!isMarkerHovered && IsItemHovered()) {
					float x = io.MousePos.X - markersOrigin.X;
					float gradientPos = x / width;
					HSB color = gradient.ColorAt(gradientPos);
					DrawMarker(markersOrigin, x, color, false);

					if (IsMouseClicked(0)) {
						gradient.Add(gradientPos, color);
						changed = true;
					}
				}
			}
		}
		PopID();

		if (changed) {
			state.isTextureStale = true;
		}

		return changed;

		void DrawMarker(Vector2 origin, float x, HSB color, bool isSelected) {
			const float Margin = 2f;

			var min = origin + vec2(x - MarkerWidth / 2, 0);
			HSB outlineColor = isSelected ? hsb(320 / 360f) : hsb(0, 0, 0.2f);

			drawList.AddTriangleFilled(
				min + vec2(MarkerWidth / 2, 0),
				min + vec2(0, MarkerHeight / 2),
				min + vec2(MarkerWidth, MarkerHeight / 2),
				outlineColor.ToU32()
			);
			drawList.AddRectFilled(
				min + vec2(0, MarkerHeight / 2),
				min + markerSize,
				outlineColor.ToU32()
			);

			drawList.AddTriangleFilled(
				min + vec2(MarkerWidth / 2, -Margin),
				min + vec2(Margin, MarkerHeight / 2),
				min + vec2(MarkerWidth - Margin, MarkerHeight / 2),
				color.ToU32()
			);
		}
	}

#endregion
}
