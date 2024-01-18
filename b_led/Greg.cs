namespace b_effort.b_led; 

// greg is secretary of state
// greg is a beast you can't tame
static partial class Greg {
	const string ProjectFile = $"test.{Project.FileExt}";

	public const int BufferWidth = 64;

	static Project project = new();

	public static List<Palette> Palettes => project.Palettes;
	public static Pattern[] Patterns => Pattern.All;
	public static List<Sequence> Sequences => project.Sequences;
	public static ClipBank[] ClipBanks => project.ClipBanks;
	
	public static ClipBank? ActiveClipBank { get; set; } = ClipBanks[0];
	public static Palette? ActivePalette => ActiveClipBank?.ActivePalette;
	public static Pattern? ActivePattern => ActiveClipBank?.ActivePattern;


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

	static Greg() {
		// init preview
		foreach (var pattern in Patterns) {
			pattern.Update();
		}
	}

	public static void SaveProject() {
		project.Save(ProjectFile);
	}

	public static void LoadProject() {
		project = Project.Load(ProjectFile);
		ActiveClipBank = ClipBanks[0];
	}
	
	public static readonly RGB[,] outputBuffer = new RGB[BufferWidth, BufferWidth];

	public static void Update() {
		var pattern = ActivePattern;
		if (pattern == null)
			return;

		pattern.Update();

		RGB[,] outputs = outputBuffer;
		HSB[,] patternPixels = pattern.pixels;
		float hueOffset = Macro.hue_offset.Value;
		var gradient = ActivePalette?.gradient;

		for (var y = 0; y < BufferWidth; y++)
		for (var x = 0; x < BufferWidth; x++) {
			HSB color = patternPixels[y, x];
			
			color.h += hueOffset;
			if (color.h > 1f)
				color.h %= 1f;
			if (gradient != null)
				color = gradient.MapColor(color);
			
			outputs[y, x] = color.ToRGB();
		}
	}
}
