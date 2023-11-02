global using ImplAttribute = System.Runtime.CompilerServices.MethodImplAttribute;
using System.Runtime.CompilerServices;

namespace b_effort.b_led;

static class MethodImplShorthand {
	public const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;
}

static class CollectionExtensions {
	public static int NextOffset<T>(this IReadOnlyCollection<T> @this, int offset) => (offset + 1) % @this.Count;
}

static class VectorShorthand {
	[Impl(Inline)] public static Vector2 vec2(float value) => new(value);
	[Impl(Inline)] public static Vector2 vec2(float x, float y) => new(x, y);
}

static class ImGuiShorthand {
	static ImGuiStylePtr? style;
	public static ImGuiStylePtr Style => style ??= ImGui.GetStyle();
	public static float FontSize => ImGui.GetFontSize();

	public static ImDrawListPtr DrawList => ImGui.GetWindowDrawList();

	[Impl(Inline)] public static float em(float value) => value * FontSize;
	[Impl(Inline)] public static Vector2 em(float x, float y) => em(new Vector2(x, y));
	[Impl(Inline)] public static Vector2 em(Vector2 value) => value * FontSize;
	[Impl(Inline)] public static int emEven(float value) => BMath.nearestEven(value * FontSize);
}

static class ImGuiUtil {
	public static void ImageTextureFit(Texture2D texture, bool center = true) {
		Vector2 area = ImGui.GetContentRegionAvail();

		float scale = area.X / texture.width;
		float y = texture.height * scale;
		if (y > area.Y) {
			scale = area.Y / texture.height;
		}

		int sizeX = (int)(texture.width * scale);
		int sizeY = (int)(texture.height * scale);

		// ImGui.SetCursorPosX(0);
		if (center) {
			// ReSharper disable PossibleLossOfFraction
			ImGui.SetCursorPosX(area.X / 2 - sizeX / 2);
			ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (area.Y / 2 - sizeY / 2));
			// ReSharper restore PossibleLossOfFraction
		}

		ImGui.Image((nint)texture.id, vec2(sizeX, sizeY));
	}
}

sealed class OopsiePoopsie : Exception {
	public OopsiePoopsie(string message) : base($"you made a fucky wucky: {message}") { }
}

#region stuff i shouldn't have written yet

enum BrightnessBlendMode {
	LAST,
	AND,
	OR,
	NOT,
	XOR,
	ADD,
	SUB,
	AVG,
}

static class BrightnessBlendModeExtensions {
	public static float Blend(this BrightnessBlendMode mode, float a, float b, float threshold = 0.01f) {
		return mode switch {
			BrightnessBlendMode.LAST => b,
			BrightnessBlendMode.AND  => b >= threshold ? a : 0,
			BrightnessBlendMode.OR   => MathF.Max(a, b),
			BrightnessBlendMode.NOT  => b >= threshold ? 0 : a,
			BrightnessBlendMode.XOR  => (a >= threshold) ^ (b >= threshold) ? MathF.Max(a, b) : 0,
			BrightnessBlendMode.ADD  => a + b,
			BrightnessBlendMode.SUB  => a - b,
			BrightnessBlendMode.AVG  => (a + b) / 2,
			_                        => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
		};
	}
}

#endregion
