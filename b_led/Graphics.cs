using System.IO;
using JetBrains.Annotations;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace b_effort.b_led.graphics;

static class glUtil {
	public static void Clear() {
		gl.ClearColor(0, 0, 0, 1f);
		gl.Clear(ClearBufferMask.ColorBufferBit);
	}
}

sealed record Texture2D : IDisposable {
	const TextureTarget Target = TextureTarget.Texture2D;

	public readonly int id;
	public readonly int width;
	public readonly int height;
	public readonly RGBA[] pixels;

	public Texture2D(
		int width,
		int height,
		TextureMinFilter minFilter = TextureMinFilter.Nearest,
		TextureMagFilter magFilter = TextureMagFilter.Nearest
	) {
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
			gl.TexParameter(Target, TextureParameterName.TextureMinFilter, (int)minFilter);
			gl.TexParameter(Target, TextureParameterName.TextureMagFilter, (int)magFilter);
		}
		gl.BindTexture(Target, 0);
	}

	~Texture2D() => this.Dispose();

	public void Dispose() {
		gl.DeleteTexture(this.id);
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

sealed record RenderTexture2D : IDisposable {
	const FramebufferTarget Target = FramebufferTarget.Framebuffer;

	public readonly int id;
	public readonly Texture2D texture;

	public int TextureId => this.texture.id;
	public int Width => this.texture.width;
	public int Height => this.texture.height;

	public RenderTexture2D(int width, int height) {
		this.id = gl.GenFramebuffer();

		gl.BindFramebuffer(Target, this.id);
		{
			this.texture = new Texture2D(width, height);
			gl.FramebufferTexture2D(
				Target, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D,
				this.texture.id, 0
			);

			var status = gl.CheckFramebufferStatus(Target);
			if (status != FramebufferErrorCode.FramebufferComplete) {
				throw new OopsiePoopsie($"Framebuffer {this.id} not initialized. Status={status}");
			}
		}
		gl.BindFramebuffer(Target, 0);
	}

	~RenderTexture2D() => this.Dispose();

	public void Dispose() {
		this.texture.Dispose();
		gl.DeleteFramebuffer(this.id);
		GC.SuppressFinalize(this);
	}

	[MustDisposeResource]
	public Context Use() => new(this);

	public readonly ref struct Context {
		public Context(RenderTexture2D rt) {
			gl.BindFramebuffer(FramebufferTarget.Framebuffer, rt.id);
			gl.Viewport(0, 0, rt.Width, rt.Height);
		}

		public void Dispose() => gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
	}
}

abstract record Shader : IDisposable {
	const string BasePath = $"{Config.AssetsPath}/shaders";

	public readonly int id;

	protected Shader(string vertPath, string fragPath) {
		int shader_vert = CompileFromFile(ShaderType.VertexShader, vertPath);
		int shader_frag = CompileFromFile(ShaderType.FragmentShader, fragPath);

		this.id = gl.CreateProgram();
		gl.AttachShader(this.id, shader_vert);
		gl.AttachShader(this.id, shader_frag);
		gl.LinkProgram(this.id);

		gl.GetProgram(this.id, GetProgramParameterName.LinkStatus, out int success);
		if (success == 0) {
			var log = gl.GetProgramInfoLog(this.id);
		  	throw new OopsiePoopsie($"Failed to link shader {this.id}\n{log}");
		}

		gl.DetachShader(this.id, shader_vert);
		gl.DetachShader(this.id, shader_frag);
		gl.DeleteShader(shader_vert);
		gl.DeleteShader(shader_frag);
	}

	static int CompileFromFile(ShaderType type, string path) {
		int id = gl.CreateShader(type);
		string source = File.ReadAllText($"{BasePath}/{path}");
		gl.ShaderSource(id, source);
		Compile(id);

		return id;
	}

	static void Compile(int id) {
		gl.CompileShader(id);
		gl.GetShader(id, ShaderParameter.CompileStatus, out int success);
		if (success == 0) {
			var log = gl.GetShaderInfoLog(id);
			throw new OopsiePoopsie($"Failed to compile shader {id}\n{log}");
		}
	}

	~Shader() => this.Dispose();

	public void Dispose() {
		gl.DeleteProgram(this.id);
		GC.SuppressFinalize(this);
	}

	[MustDisposeResource]
	public Context Use() => new(this);

	public readonly ref struct Context {
		public Context(Shader shader) => gl.UseProgram(shader.id);

		public void Dispose() => gl.UseProgram(0);
	}

	public void SetFloat(int loc, float value) => gl.ProgramUniform1(this.id, loc, value);
	public void SetInt(int loc, int value) => gl.ProgramUniform1(this.id, loc, value);
	public void SetVec2f(int loc, Vector2 value) => gl.ProgramUniform2(this.id, loc, value.ToTk());
	public void SetVec3f(int loc, Vector3 value) => gl.ProgramUniform3(this.id, loc, value.ToTk());
	public void SetVec4f(int loc, Vector4 value) => gl.ProgramUniform4(this.id, loc, value.ToTk());
}
