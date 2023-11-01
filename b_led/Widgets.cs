using System.Runtime.CompilerServices;
using static b_effort.b_led.Interop.ImGuiInternal;
using static ImGuiNET.ImGui;

namespace b_effort.b_led;

static class Widgets {
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

		Vector2 screenPos;

		bool valueChanged = false;

		PushID(label);
		PushItemWidth(width);
		BeginGroup();
		{
			uint id = GetID(label);

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

			screenPos = GetCursorScreenPos();
			InvisibleButton(label, vec2(width));

			ImGuiSliderFlags dragFlags = ImGuiSliderFlags.None;
			if ((flags & KnobFlags.DragHorizontal) == 0) {
				dragFlags |= ImGuiSliderFlags_Vertical;
			}
			if ((flags & KnobFlags.ReadOnly) == 0) {
				valueChanged = DragBehavior(
					id, dataType,
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
					valueChanged = true;
				}
			}
		}
		EndGroup();
		PopItemWidth();
		PopID();

		var center = screenPos + vec2(radius);
		var drawList = GetWindowDrawList();

		switch (variant) {
			case KnobVariant.Tick:
			{
				throw new NotImplementedException();
				break;
			}
			case KnobVariant.Dot:
			{
				throw new NotImplementedException();
				break;
			}
			case KnobVariant.Wiper:
			{
				throw new NotImplementedException();
				break;
			}
			case KnobVariant.WiperOnly:
			{
				throw new NotImplementedException();
				break;
			}
			case KnobVariant.WiperDot:
			{
				throw new NotImplementedException();
				break;
			}
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
			case KnobVariant.Space:
			{
				throw new NotImplementedException();
				break;
			}
			default: throw new ArgumentOutOfRangeException(nameof(variant), variant, null);
		}

		return valueChanged;

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
}
