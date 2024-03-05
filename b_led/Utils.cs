global using static b_effort.b_led.MethodImplShorthand;
global using static b_effort.b_led.VectorShorthand;
global using TableFlags = ImGuiNET.ImGuiTableFlags;
global using TableColFlags = ImGuiNET.ImGuiTableColumnFlags;
global using ImplAttribute = System.Runtime.CompilerServices.MethodImplAttribute;
using System.Runtime.CompilerServices;
using b_effort.b_led.graphics;

namespace b_effort.b_led;

static class MethodImplShorthand {
	public const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;
}

static class CollectionExtensions {
	public static int NextOffset<T>(this IReadOnlyCollection<T> @this, int offset) => (offset + 1) % @this.Count;

	public static unsafe int ByteSize<T>(this IReadOnlyCollection<T> @this)
		where T : unmanaged
		=> sizeof(T) * @this.Count;
}

static class VectorShorthand {
	[Impl(Inline)] public static Vector2 vec2(float value) => new(value);
	[Impl(Inline)] public static Vector2 vec2(float x, float y) => new(x, y);
}

static class VectorExtensions {
	public static tkVector2 ToTk(this Vector2 @this) => new(@this.X, @this.Y);
	public static tkVector3 ToTk(this Vector3 @this) => new(@this.X, @this.Y, @this.Z);
	public static tkVector4 ToTk(this Vector4 @this) => new(@this.X, @this.Y, @this.Z, @this.W);
	public static Vector2 ToNative(this tkVector2 @this) => new(@this.X, @this.Y);
	public static Vector3 ToNative(this tkVector3 @this) => new(@this.X, @this.Y, @this.Z);
	public static Vector4 ToNative(this tkVector4 @this) => new(@this.X, @this.Y, @this.Z, @this.W);

	public static Vector2 Add(this Vector2 @this, float x = 0f, float y = 0f) {
		if (x != 0f) @this.X += x;
		if (y != 0f) @this.Y += y;

		return @this;
	}

	public static Vector2 Floor(this Vector2 @this) => vec2(MathF.Floor(@this.X), MathF.Floor(@this.Y));
}

static class MiscExtensions {
	public static string GetDerivedNameFromType(this object @this, string? trimPrefix = null) {
		var type = @this.GetType();
		trimPrefix ??= type.BaseType!.Name;
		return type.Name.Replace(trimPrefix, null).Trim('_');
	}
}

static class ImGuiShorthand {
	static ImGuiStylePtr? style;
	public static ImGuiStylePtr Style { [Impl(Inline)] get => style ??= ImGui.GetStyle(); }

	[Impl(Inline)] public static Vector2 ContentAvail() => ImGui.GetContentRegionAvail();

	[Impl(Inline)] public static float em(float value) => value * ImGui.GetFontSize();
	[Impl(Inline)] public static Vector2 em(float x, float y) => em(new Vector2(x, y));
	[Impl(Inline)] public static Vector2 em(Vector2 value) => value * ImGui.GetFontSize();
	[Impl(Inline)] public static int emInt(float value) => (int)MathF.Floor(value * ImGui.GetFontSize());
	[Impl(Inline)] public static int emEven(float value) => BMath.nearestEven(value * ImGui.GetFontSize());
	[Impl(Inline)] public static int emOdd(float value) => BMath.nearestOdd(value * ImGui.GetFontSize());

	public static void SpacingY(float height) => ImGui.Dummy(vec2(0, height));

	public static bool InputIntClamp(string label, ref int v, int min, int max, int step = 1, int step_fast = 10) {
		if (ImGui.InputInt(label, ref v, step, step_fast)) {
			v = Math.Clamp(v, min, max);
			return true;
		}
		return false;
	}
}

static class ImGuiUtil {
	public static void ImageTextureFit(Texture2D texture, bool center = true) {
		Vector2 area = ImGui.GetContentRegionAvail();
		// area.X = ImGui.CalcItemWidth();

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

		ImGui.Image(texture.id, vec2(sizeX, sizeY));
	}

	public static void AddImageOrEmpty(this ImDrawListPtr drawList, nint? textureId, Vector2 p_min, Vector2 p_max) {
		uint bgColor = ImGui.GetColorU32(ImGuiCol.WindowBg);

		if (textureId.HasValue) {
			drawList.AddImage(textureId.Value, p_min, p_max);
		} else {
			drawList.AddRectFilled(p_min, p_max, bgColor);
		}
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
