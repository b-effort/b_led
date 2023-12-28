global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Numerics;
global using Color = b_effort.b_led.Color;
global using Raylib_cs;
global using rl = Raylib_cs.Raylib;
global using rlColor = Raylib_cs.Color;
global using rlImGui_cs;
global using ImGuiNET;
global using static b_effort.b_led.Color;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using b_effort.b_led;

/*

# Runway
- ! sequences
- gather config constants


# Mapping
https://electromage.com/docs/intro-to-mapping
1u = 1cm
([0, 1]], [0, 1])

buffers are [y, x] = [y * width + x]
 */

const int FPS = 144;
int width = 1280;
int height = 1280;

#region init

rl.SetConfigFlags(ConfigFlags.FLAG_WINDOW_RESIZABLE);
rl.InitWindow(width, height, "b_led");
rl.SetTargetFPS(FPS);

rlImGui.SetupUserFonts = ImFonts.SetupUserFonts;
rlImGui.Setup(enableDocking: true);
// ImGui.GetStyle().ScaleAllSizes(1.30f);

ImGui.SetColorEditOptions(
	ImGuiColorEditFlags.NoAlpha
  | ImGuiColorEditFlags.Float
  | ImGuiColorEditFlags.InputHSV | ImGuiColorEditFlags.DisplayHSV
);

#endregion

#region app setup

try {
	Greg.LoadProject();
} catch (Exception e) {
	Console.WriteLine(e);
	Greg.LoadDemoProject();
}

using var previewWindow = new PreviewWindow();
var palettesWindow = new PalettesWindow();
var patternsWindow = new PatternsWindow();
var sequencesWindow = new SequencesWindow();
var clipsWindow = new ClipsWindow();
var metronomeWindow = new MetronomeWindow();
var macrosWindow = new MacrosWindow();
var pushWindow = new PushWindow();

var funcPlotterWindow = new FuncPlotterWindow {
	Funcs = new() {
		("saw", PatternScript.saw),
		("sine", PatternScript.sine),
		("triangle", PatternScript.triangle),
		("square", PatternScript.square),
	},
};

Push2.Connect();
FixtureServer.Start();

#endregion

var deltaTimer = Stopwatch.StartNew();
float deltaTime = 1f / FPS;

while (!rl.WindowShouldClose()) {
	if (rl.IsWindowResized()) {
		width = rl.GetRenderWidth();
		height = rl.GetRenderHeight();
	}
	
	Update(deltaTime);

	rl.BeginDrawing();
	{
		rl.ClearBackground(rlColor.BLACK);

		rlImGui.Begin(deltaTime);
		DrawUI();
		rlImGui.End();
		rl.DrawFPS(width - 84, 4);
	}
	rl.EndDrawing();
	
	deltaTime = (float)deltaTimer.Elapsed.TotalSeconds;
	deltaTimer.Restart();
}

Push2.Dispose();
rlImGui.Shutdown();
rl.CloseWindow();
return;

void Update(float deltaTime) {
	Push2.Update();
	Metronome.Tickle(deltaTime);
	Greg.Update();

	FixtureServer.Update(deltaTime);
}

void DrawUI() {
	ImGui.DockSpaceOverViewport();
	
	if (ImGui.BeginMainMenuBar()) {
		if (ImGui.BeginMenu("File")) {
			if (ImGui.MenuItem("Save")) {
				Greg.SaveProject();
			}
			if (ImGui.MenuItem("Load")) {
				Greg.LoadProject();
			}
			if (ImGui.MenuItem("Load demo project")) {
				Greg.LoadDemoProject();
			}
		}
	}
	
	previewWindow.Show();
	palettesWindow.Show();
	clipsWindow.Show();
	sequencesWindow.Show();
	patternsWindow.Show();
	metronomeWindow.Show();
	macrosWindow.Show();
	// funcPlotterWindow.Show();
	pushWindow.Show();
}

static class ImFonts {
	const string JetBrainsMono_Regular_TTF = "assets/JetBrainsMono-Regular.ttf";

	public static ImFontPtr Default { get; private set; }

	public static ImFontPtr Mono_15 { get; private set; }
	public static ImFontPtr Mono_17 { get; private set; }

	public static unsafe void SetupUserFonts(ImGuiIOPtr io) {
		io.Fonts.Clear();

		ImFontConfig fontCfg = new ImFontConfig {
			OversampleH = 3,
			OversampleV = 3,
			PixelSnapH = 1,
			RasterizerMultiply = 1,
			GlyphMaxAdvanceX = float.MaxValue,
			FontDataOwnedByAtlas = 1,
		};

		Mono_17 = io.LoadTTF(JetBrainsMono_Regular_TTF, 17, &fontCfg);
		rlImGui.LoadFontAwesome(px_to_pt(13));
		Mono_15 = io.LoadTTF(JetBrainsMono_Regular_TTF, 15, &fontCfg);

		Default = Mono_17;
	}

	static ImFontPtr LoadTTF(this ImGuiIOPtr io, string file, int px, ImFontConfigPtr config)
		=> io.Fonts.AddFontFromFileTTF(file, px_to_pt(px), config);

	static int px_to_pt(int px) => px * 96 / 72;
}

// struct LEDMap {
// 	public required string name;
// 	public required Vector2[] leds;
// }

// delegate LEDMap LEDMapper(int numPixels);

static class DragDropType {
	public const string Palette = "palette";
	public const string Pattern = "pattern";
}

// greg is secretary of state
// greg is a beast you can't tame
static class Greg {
	const string ProjectFile = $"test.{Project.FileExt}";

	public const int BufferWidth = 64;

	static Project project = new();

	public static List<Palette> Palettes => project.Palettes;
	public static Pattern[] Patterns => Pattern.All;
	public static List<Sequence> Sequences => project.Sequences;
	public static ClipBank[] ClipBanks => project.ClipBanks;
	
	public static ClipBank? ActiveClipBank { get; set; } = ClipBanks[0];
	public static Palette? ActivePalette => (Palette?)ActiveClipBank?.ActivePaletteClip?.Contents;
	public static Pattern? ActivePattern => (Pattern?)ActiveClipBank?.ActivePatternClip?.Contents;


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
	
	// public static LEDMapper[] LEDMappers { get; set; } = Array.Empty<LEDMapper>();

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
			color.h = MathF.Abs(color.h + hueOffset) % 1f;
			if (gradient != null) {
				color = gradient.MapColor(color);
			}
			outputs[y, x] = color.ToRGB();
		}
	}
}

interface ClipContents {
	string Id { get; }
	nint? TextureId { get; }
}

[DataContract]
sealed class Clip {
	enum ContentsType {
		Empty,
		Palette,
		Pattern,
		Sequence,
	}
	
	[DataMember] ContentsType contentsType;
	[DataMember] string? contentsId;

	ClipContents? contents;
	public ClipContents? Contents {
		get => this.contents;
		set {
			this.contents = value;
			this.contentsType = this.contents switch {
				null     => ContentsType.Empty,
				Palette  => ContentsType.Palette,
				Pattern  => ContentsType.Pattern,
				Sequence => ContentsType.Sequence,
				_        => throw new ArgumentOutOfRangeException(),
			};
			this.contentsId = value?.Id;
		}
	}

	public bool HasContents => this.Contents != null;

	internal void InitContents(Project project) {
		switch (this.contentsType) {
			case ContentsType.Empty: break;
			case ContentsType.Palette:
				this.Contents = project.Palettes.First(p => p.Id == this.contentsId);
				break;
			case ContentsType.Pattern:
				this.Contents = Pattern.FromId(this.contentsId!);
				break;
			default: throw new ArgumentOutOfRangeException();
		}
	}
}

[DataContract]
sealed class ClipBank {
	public const int NumCols = 8;
	public const int NumRows = 8;

	[DataMember] public string name;
	[DataMember] public readonly Clip[][] clips;
	
	public ClipBank(string name) {
		this.name = name;
		
		this.clips = new Clip[NumRows][];
		for (var y = 0; y < NumRows; y++) {
			this.clips[y] = new Clip[NumCols];
			for (var x = 0; x < NumCols; x++) {
				this.clips[y][x] = new Clip();
			}
		}
	}

	[JsonConstructor]
	public ClipBank(string name, Clip[][] clips) {
		this.name = name;
		
		int rows = clips.Length;
		int cols = clips[0].Length;
		if (rows != NumRows || cols != NumCols) {
			throw new Exception($"Invalid clips dimensions. rows={rows}, cols={cols}");
		}
		this.clips = clips;
	}

	internal void InitClips(Project project) {
		foreach (var clip in this.clips.SelectMany(x => x)) {
			clip.InitContents(project);
		}
	}
	
	public Clip? ActivePaletteClip { get; private set; }
	public Clip? ActivePatternClip { get; private set; }

	public IEnumerable<Clip> ActiveClips {
		get {
			var clip = this.ActivePaletteClip;
			if (clip != null) yield return clip;
			clip = this.ActivePatternClip;
			if (clip != null) yield return clip;
		}
	}

	public bool Activate(Clip clip) {
		if (!clip.HasContents)
			return false;

		switch (clip.Contents) {
			case Palette:
				this.ActivePaletteClip = clip;
				break;
			case Pattern:
			case Sequence:
				this.ActivePatternClip = clip;
				break;
			default: throw new ArgumentOutOfRangeException();
		}

		return true;
	}

	public bool IsActive(Clip clip) => clip.HasContents && this.ActiveClips.Contains(clip);
}
