using b_effort.b_led.graphics;

namespace b_effort.b_led.resources;

static class Shaders {
	public static readonly Shader_FixturePreview FixturePreview = new();

	public static void Unload() {
		FixturePreview.Dispose();
	}
}

sealed record Shader_FixturePreview() : Shader(
	"fixture_preview.vert",
	"fixture_preview.frag"
) {
	const int Loc_Bounds = 0;

	public void Bounds(Vector2 value) => this.SetVec2f(Loc_Bounds, value);
}
