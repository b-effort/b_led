using b_effort.b_led.graphics;

namespace b_effort.b_led.resources;

static class ImFonts {
	const string JetBrainsMono_Regular_TTF = $"{Config.FontsPath}/JetBrainsMono-Regular.ttf";

	public static ImFontPtr Default { get; private set; }

	public static ImFontPtr Mono_15 { get; private set; }
	public static ImFontPtr Mono_17 { get; private set; }

	public static unsafe void LoadFonts(ImGuiIOPtr io) {
		var config = new ImFontConfig {
			OversampleH = 3,
			OversampleV = 3,
			PixelSnapH = 1,
			RasterizerMultiply = 1,
			GlyphMaxAdvanceX = float.MaxValue,
			FontDataOwnedByAtlas = 1,
		};

		Mono_17 = io.LoadTTF(JetBrainsMono_Regular_TTF, 17, &config);
		FontAwesome6.Load(io, px_to_pt(13));
		Mono_15 = io.LoadTTF(JetBrainsMono_Regular_TTF, 15, &config);

		Default = Mono_17;
	}

	static ImFontPtr LoadTTF(this ImGuiIOPtr io, string file, int px, ImFontConfigPtr config)
		=> io.Fonts.AddFontFromFileTTF(file, px_to_pt(px), config);

	static int px_to_pt(int px) => px * 96 / 72;
}

static class Shaders {
	public static readonly Shader_FixturePreview FixturePreview = new();

	public static void Unload() {
		FixturePreview.Dispose();
	}
}

sealed record Shader_FixturePreview : Shader {
	readonly int loc_projection;

	public Shader_FixturePreview() : base(
		"fixture_preview.vert",
		"fixture_preview.frag"
	) {
		this.loc_projection = this.Loc("projection");
	}

	public void Projection(ref Matrix4 value) => this.SetMat4(this.loc_projection, ref value);
}

sealed record Shader_WorldPreview : Shader {
	readonly int loc_bounds;
	readonly int loc_projection;

	public Shader_WorldPreview() : base(
		"world_preview.vert",
		"world_preview.frag"
	) {
		this.loc_bounds = this.Loc("bounds");
		this.loc_projection = this.Loc("projection");
	}

	public void Bounds(Vector2 value) => this.SetVec2f(this.loc_bounds, value);
	public void Projection(ref Matrix4 value) => this.SetMat4(this.loc_projection, ref value);
}
