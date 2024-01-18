global using static b_effort.b_led.MethodImplShorthand;
global using static b_effort.b_led.VectorShorthand;
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

static class VectorExtensions {
	public static Vector2 Floor(this Vector2 @this) => vec2(MathF.Floor(@this.X), MathF.Floor(@this.Y));
}

static class MiscExtensions {
	public static string GetDerivedNameFromType(this object @this) {
		var type = @this.GetType();
		return type.Name.Replace(type.BaseType!.Name, null).Trim('_');
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

	public static void AddImageOrEmpty(this ImDrawListPtr drawList, nint? textureId, Vector2 p_min, Vector2 p_max) {
		uint bgColor = ImGui.GetColorU32(ImGuiCol.WindowBg);
		
		if (textureId.HasValue) {
			drawList.AddImage(textureId.Value, p_min, p_max);
		} else {
			drawList.AddRectFilled(p_min, p_max, bgColor);
		}
	}
}

static class RaylibUtil {
	public static unsafe Texture2D CreateTexture(int width, int height, out rlColor[] pixels) {
		const PixelFormat pixelFormat = PixelFormat.PIXELFORMAT_UNCOMPRESSED_R8G8B8A8;

		Texture2D texture = new Texture2D {
			width = width,
			height = height,
			format = pixelFormat,
			mipmaps = 1,
		};

		pixels = new rlColor[width * height];
		fixed (void* data = pixels) {
			texture.id = Rlgl.rlLoadTexture(data, width, height, pixelFormat, 1);
		}

		return texture;
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
