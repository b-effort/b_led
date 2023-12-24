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
using System.Text.Json.Serialization;
using b_effort.b_led;

/*

# Runway
- clip launching
- project files


# Mapping
https://electromage.com/docs/intro-to-mapping
1u = 1cm
([0, 1]], [0, 1])

buffers are [y, x] = [y * width + x]
 */

const int FPS = 144;
const int Width = 1280;
const int Height = 1280;

#region init

rl.SetConfigFlags(ConfigFlags.FLAG_WINDOW_RESIZABLE);
rl.InitWindow(Width, Height, "b_led");
rl.SetTargetFPS(FPS);

rlImGui.SetupUserFonts = ImFonts.SetupUserFonts;
rlImGui.Setup(enableDocking: true);
ImGui.GetStyle().ScaleAllSizes(1.30f);

ImGui.SetColorEditOptions(
	ImGuiColorEditFlags.NoAlpha
  | ImGuiColorEditFlags.Float
  | ImGuiColorEditFlags.InputHSV | ImGuiColorEditFlags.DisplayHSV
);

#endregion

#region app setup

using var previewWindow = new PreviewWindow();
var palettesWindow = new PalettesWindow();
var patternsWindow = new PatternsWindow();
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
	Update(deltaTime);

	rl.BeginDrawing();
	{
		rl.ClearBackground(rlColor.BLACK);

		rlImGui.Begin(deltaTime);
		DrawUI();
		rlImGui.End();
		rl.DrawFPS(Width - 84, 4);
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
	Metronome.Tick();
	State.Update();

	FixtureServer.Update(deltaTime);
}

void DrawUI() {
	ImGui.DockSpaceOverViewport();
	
	if (ImGui.BeginMainMenuBar()) {
		if (ImGui.BeginMenu("File")) {
			if (ImGui.MenuItem("Save")) {
				State.SaveProject();
			}
			if (ImGui.MenuItem("Load")) {
				State.LoadProject();
			}
			if (ImGui.MenuItem("Load demo project")) {
				State.LoadDemoProject();
			}
		}
	}
	
	previewWindow.Show();
	palettesWindow.Show();
	clipsWindow.Show();
	patternsWindow.Show();
	metronomeWindow.Show();
	macrosWindow.Show();
	funcPlotterWindow.Show();
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

static class State {
	const string ProjectFile = $"test.{Project.FileExt}";

	public const int BufferWidth = 64;

	static Project project = new();

	public static List<Palette> Palettes => project.Palettes;
	public static Palette? ActivePalette { get; set; }
	
	public static Pattern[] Patterns { get; }
	public static Pattern? ActivePattern { get; set; }

	public static ClipBank[] ClipBanks => project.ClipBanks;
	public static ClipBank? SelectedClipBank { get; set; }

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
			ClipBanks = new ClipBank[] {
				new("bank 1"),
				new("bank 2"),
				new("bank 3"),
				new("bank 4"),
				new("bank 5"),
				new("bank 6"),
				new("bank 7"),
				new("bank 8"),
			},
		};
		
		ActivePalette = Palettes[2];
		SelectedClipBank = ClipBanks[0];
	}

	static State() {
		Patterns = AppDomain.CurrentDomain.GetAssemblies()
			.SelectMany(a => a.GetTypes())
			.Where(t => t.IsSealed && typeof(Pattern).IsAssignableFrom(t))
			.Select(t => (Pattern)Activator.CreateInstance(t)!)
			.ToArray();
		// init preview
		foreach (var pattern in Patterns) {
			pattern.Update();
		}
		ActivePattern = Patterns[0];
	}

	public static void SaveProject() {
		project.Save(ProjectFile);
	}

	public static void LoadProject() {
		project = Project.Load(ProjectFile);
		SelectedClipBank = ClipBanks[0];
	}
	
	// public static LEDMapper[] LEDMappers { get; set; } = Array.Empty<LEDMapper>();

	public static readonly RGB[,] outputBuffer = new RGB[BufferWidth, BufferWidth];

	public static void Update() {
		if (ActivePattern is null)
			return;

		ActivePattern.Update();

		RGB[,] outputs = outputBuffer;
		HSB[,] patternPixels = ActivePattern.pixels;
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
}

enum ClipType {
	Empty = 0,
	Palette = 1,
	Pattern = 2,
}

sealed class Clip {
	[JsonInclude] public ClipType Type { get; set; }
	[JsonInclude] public string? ContentsId { get; set; }

	ClipContents? contents;
	[JsonIgnore] public ClipContents? Contents {
		get => this.contents;
		set {
			this.contents = value;
			this.Type = this.contents switch {
				null    => ClipType.Empty,
				Palette => ClipType.Palette,
				Pattern => ClipType.Pattern,
				_       => throw new ArgumentOutOfRangeException(),
			};
			this.ContentsId = value?.Id;
		}
	}


	public bool HasContents => this.Contents != null;

	public bool IsActive => this.Contents switch {
		null            => false,
		Palette palette => State.ActivePalette == palette,
		Pattern pattern => State.ActivePattern == pattern,
		_               => throw new ArgumentOutOfRangeException(),
	};

	internal void LoadContents(Project project) {
		switch (this.Type) {
			case ClipType.Empty: break;
			case ClipType.Palette:
				this.Contents = project.Palettes.First(p => p.Id == this.ContentsId);
				break;
			case ClipType.Pattern:
				this.Contents = State.Patterns.First(p => p.Id == this.ContentsId);
				break;
			default: throw new ArgumentOutOfRangeException();
		}
	}

	public void Activate() {
		if (this.Contents is null)
			return;

		switch (this.Contents) {
			case Palette palette:
				State.ActivePalette = palette;
				break;
			case Pattern pattern:
				State.ActivePattern = pattern;
				break;
			default: throw new ArgumentOutOfRangeException();
		}
	}
}

sealed class ClipBank {
	public const int NumCols = 8;
	public const int NumRows = 8;

	[JsonInclude] public string name;
	[JsonInclude] public readonly Clip[][] clips;

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
}
