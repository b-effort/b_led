global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Numerics;
global using Vector2 = System.Numerics.Vector2;
global using ImGuiNET;
global using fftw = SharpFFTW.Single;
global using gl = OpenTK.Graphics.OpenGL4.GL;
global using static b_effort.b_led.Color;

using System.Diagnostics;
using System.Threading;
using b_effort.b_led;
using b_effort.b_led.interop;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Graphics.OpenGL4;
using OpenTKUtils = OpenTK.Core.Utils;

/*

# Runway
- sequences
	- create/delete
	- time division
	- macro keyframes
- clip banks
	- clip dragging
	- rename
	- switch banks
	- map to push
- metronome pause
- ! led mapping
- sound reactive
	- fft
- ui
	- remember selected tabs
	- window opening/closing
- housekeeping
	- group ui into files by category (it's finally time)
	- gather config constants

# Reminders
buffers are [y, x] = [y * width + x]

 */

if (OperatingSystem.IsWindows()) {
	Thread.CurrentThread.SetApartmentState(ApartmentState.Unknown);
	Thread.CurrentThread.SetApartmentState(ApartmentState.STA);
	kernel32.SetThreadAffinityMask(kernel32.GetCurrentThread(), 1);
}

using var window = new MainWindow(
	width: 1280,
	height: 1280,
	fps: 144
);
window.Run();

sealed class MainWindow : NativeWindow {
	readonly int fps;

	readonly PreviewWindow previewWindow;
	readonly PalettesWindow palettesWindow;
	readonly PatternsWindow patternsWindow;
	readonly SequencesWindow sequencesWindow;
	readonly ClipsWindow clipsWindow;
	readonly FixturesWindow fixturesWindow;
	readonly AudioWindow audioWindow;
	readonly MetronomeWindow metronomeWindow;
	readonly MacrosWindow macrosWindow;
	readonly PushWindow pushWindow;
	readonly FuncPlotterWindow funcPlotterWindow;

	public unsafe MainWindow(int width, int height, int fps) : base(
		new NativeWindowSettings {
			Title = "b_led",
			ClientSize = (width, height),
			Vsync = VSyncMode.Off,
		}
	) {
		this.fps = fps;

		{ // # init imgui
			ImGui.CreateContext();

			var io = ImGui.GetIO();
			io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
			ImFonts.LoadFonts(io);

			ImGui_Glfw.InitForOpenGL(this.WindowPtr, true);
			var glVersion = gl.GetString(StringName.Version);
			var glslVersion = gl.GetString(StringName.ShadingLanguageVersion);
			Console.WriteLine($"GL Version: {glVersion}");
			Console.WriteLine($"GLSL Version: {glslVersion}");
			ImGui_OpenGL3.Init();

			ImGui.StyleColorsDark();
			var style = ImGui.GetStyle();
			style.ItemInnerSpacing = vec2(8, 4);

			// ImGui.GetStyle().ScaleAllSizes(1.30f);

			ImGui.SetColorEditOptions(
				ImGuiColorEditFlags.NoAlpha
			  | ImGuiColorEditFlags.Float
			  | ImGuiColorEditFlags.InputHSV | ImGuiColorEditFlags.DisplayHSV
			);
		}

		// # load project
		try {
			Greg.LoadProject();
		} catch (Exception e) {
			Console.WriteLine(e);
			Greg.LoadDemoProject();
		}

		// # create windows
		this.previewWindow = new PreviewWindow();
		this.palettesWindow = new PalettesWindow();
		this.patternsWindow = new PatternsWindow();
		this.sequencesWindow = new SequencesWindow();
		this.clipsWindow = new ClipsWindow();
		this.fixturesWindow = new FixturesWindow();
		this.audioWindow = new AudioWindow();
		this.metronomeWindow = new MetronomeWindow();
		this.macrosWindow = new MacrosWindow();
		this.pushWindow = new PushWindow();

		this.funcPlotterWindow = new FuncPlotterWindow {
			Funcs = new() {
				("saw", PatternScript.saw),
				("sine", PatternScript.sine),
				("triangle", PatternScript.triangle),
				("square", PatternScript.square),
			},
		};

		Push2.Connect();
		// FixtureServer.Start();
	}

	public override void Dispose() {
		AudioIn.Close();
		Push2.Dispose();
		// Shaders.Unload();

		ImGui_OpenGL3.Shutdown();
		ImGui_Glfw.Shutdown();
		ImGui.DestroyContext();

		base.Dispose();
	}

	public unsafe void Run() {
		if (OperatingSystem.IsWindows()) {
			winmm.timeBeginPeriod(Config.SleepSchedulerPeriod);
		}

		double targetFrameTime = 1f / this.fps;
		var deltaTimer = Stopwatch.StartNew();

		while (!GLFW.WindowShouldClose(this.WindowPtr)) {
			double deltaTime = deltaTimer.Elapsed.TotalSeconds;
			float deltaTimeF = (float)deltaTime;

			if (deltaTime > targetFrameTime) {
				deltaTimer.Restart();

				this.NewInputFrame();
				GLFW.PollEvents();
				GLFW.MakeContextCurrent(this.WindowPtr);
				var io = ImGui.GetIO();

				ImGui_OpenGL3.NewFrame();
				ImGui_Glfw.NewFrame();
				ImGui.NewFrame();

				// if (rl.IsWindowResized()) {
				// 	width = rl.GetRenderWidth();
				// 	height = rl.GetRenderHeight();
				// }

				this.Update(deltaTimeF);
				this.RenderUI();
				// rl.DrawFPS(width - 84, 4);

				ImGui.Render();
				gl.Viewport(0, 0, this.ClientSize.X, this.ClientSize.Y);
				gl.ClearColor(0, 0, 0, 1f);
				gl.Clear(ClearBufferMask.ColorBufferBit);
				var dd = ImGui.GetDrawData();
				// ImGui_OpenGL3.RenderDrawData(ImGuiNative.igGetDrawData());
				ImGui_OpenGL3.RenderDrawData(dd.NativePtr);
				GLFW.SwapBuffers(this.WindowPtr);
			}

			double remainingTime = targetFrameTime - deltaTime;
			OpenTKUtils.AccurateSleep(remainingTime, Config.SleepSchedulerPeriod);
		}

		if (OperatingSystem.IsWindows()) {
			winmm.timeBeginPeriod(Config.SleepSchedulerPeriod);
		}
	}

	void Update(float deltaTime) {
		Push2.Update();
		Metronome.Tickle(deltaTime);
		Greg.Update();

		FixtureServer.Update(deltaTime);
	}

	void RenderUI() {
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

		this.previewWindow.Show();
		this.palettesWindow.Show();
		this.clipsWindow.Show();
		this.sequencesWindow.Show();
		this.patternsWindow.Show();
		this.fixturesWindow.Show();
		this.audioWindow.Show();
		this.metronomeWindow.Show();
		this.macrosWindow.Show();
		// this.funcPlotterWindow.Show();
		this.pushWindow.Show();
	}
}

static class Config {
	public const string AssetsPath = "assets";

	public static readonly Vector2 FullPreviewResolution = vec2(800, 600);
	public static readonly Vector2 PatternPreviewResolution = vec2(64);

	// https://github.com/opentk/opentk/blob/f3539ad1fda98af9b265e941e0aecfb4662a9bbe/src/OpenTK.Windowing.Desktop/GameWindow.cs#L236
	public const int SleepSchedulerPeriod = 8;
}

static class ImFonts {
	const string JetBrainsMono_Regular_TTF = $"{Config.AssetsPath}/JetBrainsMono-Regular.ttf";

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
		IconFonts.FontAwesome6.Load(io, px_to_pt(13));
		Mono_15 = io.LoadTTF(JetBrainsMono_Regular_TTF, 15, &config);

		Default = Mono_17;
	}

	static ImFontPtr LoadTTF(this ImGuiIOPtr io, string file, int px, ImFontConfigPtr config)
		=> io.Fonts.AddFontFromFileTTF(file, px_to_pt(px), config);

	static int px_to_pt(int px) => px * 96 / 72;
}

// static class Shaders {
// 	const string ShadersPath = $"{Config.AssetsPath}/shaders";
//
// 	public static readonly Shader FixturePreview = rl.LoadShader(
// 		$"{ShadersPath}/fixture_preview.vert",
// 		$"{ShadersPath}/fixture_preview.frag"
// 	);
// 	public static readonly int FixturePreview_Uniform_Bounds = rl.GetShaderLocation(FixturePreview, "bounds");
//
// 	public static void Unload() {
// 		rl.UnloadShader(FixturePreview);
// 	}
// }
