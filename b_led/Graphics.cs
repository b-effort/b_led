using OpenTK.Graphics.OpenGL4;

namespace b_effort.b_led.graphics;

static class glUtil {
}

/*
static class rlUtil {
	public static unsafe Texture2D CreateTexture(Vector2 size, out rlColor[] pixels) =>
		CreateTexture((int)size.X, (int)size.Y, out pixels);
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
 */

sealed record Texture2D : IDisposable {
	const TextureTarget Target = TextureTarget.Texture2D;

	public int id { get; private set; }
	public readonly int width;
	public readonly int height;
	public readonly RGBA[] pixels;

	public Texture2D(int width, int height) {
		this.id = gl.GenTexture();
		this.width = width;
		this.height = height;
		this.pixels = new RGBA[width * height];

		gl.BindTexture(Target, this.id);
		{
			gl.TexStorage2D(TextureTarget2d.Texture2D, 1, SizedInternalFormat.Rgba8, width, height);
			const int wrapMode = (int)TextureWrapMode.Repeat;
			gl.TexParameter(Target, TextureParameterName.TextureWrapS, wrapMode);
			gl.TexParameter(Target, TextureParameterName.TextureWrapT, wrapMode);
			// float[] borderColor = { 0f, 1f, 0f, 1f };
			// gl.TexParameter(Target, TextureParameterName.TextureBorderColor, borderColor);
			gl.TexParameter(Target, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
			gl.TexParameter(Target, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
		}
		gl.BindTexture(Target, 0);
	}

	~Texture2D() => this.Dispose();

	public bool IsLoaded => this.id > 0;

	public void Dispose() {
		if (this.IsLoaded) {
			gl.DeleteTexture(this.id);
			this.id = -1;
		}
		GC.SuppressFinalize(this);
	}

	public void Update() {
		gl.BindTexture(Target, this.id);
		{
			gl.TexSubImage2D(
				Target, 0, 0, 0, this.width, this.height,
				PixelFormat.Rgba, PixelType.UnsignedByte, this.pixels
			);
		}
		gl.BindTexture(Target, 0);
	}
}
