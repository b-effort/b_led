namespace b_effort.b_led;

// greg is secretary of state
// greg is a beast you can't tame
static class Greg {
	const string ProjectFile = $"test.{Project.FileExt}";
	static Project project = new();

	static Greg() {
		// previewTexture = rlUtil.CreateTexture(Config.FullPreviewResolution, out previewPixels);

		foreach (var pattern in Patterns) {
			pattern.UpdatePreview();
		}
	}

	public static List<Palette> Palettes => project.Palettes;
	public static Pattern[] Patterns => Pattern.All;
	public static List<Sequence> Sequences => project.Sequences;

	public static ClipBank[] ClipBanks => project.ClipBanks;
	public static ClipBank? ActiveClipBank { get; set; } = ClipBanks[0];
	public static Palette? ActivePalette => ActiveClipBank?.ActivePalette;
	public static Pattern? ActivePattern => ActiveClipBank?.ActivePattern;


#region project

	public static void LoadDemoProject() {
		project = new Project {
			Palettes = {
				new("b&w"),
				new("rainbow", new(
					new Gradient.Point[] {
						new(0f, hsb(0f)),
						new(1f, hsb(1f)),
					}
				)),
				new("cyan-magenta", new(
					new Gradient.Point[] {
						new(0f, hsb(170 / 360f)),
						new(1f, hsb(320 / 360f)),
					}
				)),
			},
		};
		ActiveClipBank = ClipBanks[0];
	}

	public static void SaveProject() {
		project.Save(ProjectFile);
	}

	public static void LoadProject() {
		project = Project.Load(ProjectFile);
		ActiveClipBank = ClipBanks[0];
	}

#endregion

#region fixtures
	// I could make a separate FixtureManager ...but I hate that
	// anyways, greg's more than up for the task

	public static FixtureTemplate[] FixtureTemplates => FixtureTemplate.All;
	public static List<Fixture> Fixtures => project.Fixtures;
	public static Rect WorldRect { get; private set; }

	public static void AddFixture(Fixture fixture) => Fixtures.Add(fixture);

	public static void UpdateWorldRect() {
		Rect worldRect = new(pos: Vector2.Zero, size: Vector2.Zero);

		foreach (var fixture in Fixtures) {
			Rect fixtureRect = new(
				pos: fixture.worldPos - (fixture.Bounds * fixture.anchorPoint),
				size: fixture.Bounds
			);

			worldRect.Expand(fixtureRect);
		}

		WorldRect = worldRect;
	}

#endregion

	public const int BufferWidth = 64;

	public static void Update() {
		var pattern = ActivePattern;
		if (pattern == null)
			return;

		pattern.Tick();
		pattern.UpdatePreview();

		var palette = ActivePalette;
		foreach (var fixture in Fixtures) {
			fixture.Render(pattern, palette);
		}
	}
}
