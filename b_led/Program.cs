global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Numerics;
global using Vector2 = System.Numerics.Vector2;
global using tkVector2 = OpenTK.Mathematics.Vector2;
global using tkVector3 = OpenTK.Mathematics.Vector3;
global using tkVector4 = OpenTK.Mathematics.Vector4;
global using Matrix4 = OpenTK.Mathematics.Matrix4;
global using ImGuiNET;
global using fftw = SharpFFTW.Single;
global using gl = OpenTK.Graphics.OpenGL4.GL;
global using static b_effort.b_led.Color;

using System.Diagnostics;
using System.Threading;
using b_effort.b_led;
using b_effort.b_led.graphics;
using b_effort.b_led.interop;
using b_effort.b_led.resources;
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

static class Config {
	public const string ResourcesPath = "resources";
	public const string FontsPath = $"{ResourcesPath}/fonts";
	public const string ShadersPath = $"{ResourcesPath}/shaders";

	public static readonly Vector2 FullPreviewResolution = vec2(800, 600);
	public static readonly Vector2 PatternPreviewResolution = vec2(64);
	public static readonly Vector2 FixturePreviewResolution = vec2(512);

	// https://github.com/opentk/opentk/blob/f3539ad1fda98af9b265e941e0aecfb4662a9bbe/src/OpenTK.Windowing.Desktop/GameWindow.cs#L236
	public const int SleepSchedulerPeriod = 8;
}


sealed class MainWindow : NativeWindow {
	readonly int fps;

	readonly PreviewWindow win_preview;
	readonly PalettesWindow win_palettes;
	readonly PatternsWindow win_patterns;
	readonly SequencesWindow win_sequences;
	readonly ClipsWindow win_clips;
	readonly FixturesWindow win_fixtures;
	readonly AudioWindow win_audio;
	readonly MetronomeWindow win_metronome;
	readonly MacrosWindow win_macros;
	readonly PushWindow win_push;
	readonly FuncPlotterWindow win_funcPlotter;

	public unsafe MainWindow(int width, int height, int fps) : base(
		new NativeWindowSettings {
			Title = "b_led",
			ClientSize = (width, height),
			Vsync = VSyncMode.Off,
			APIVersion = new(4, 6),
		}
	) {
		this.fps = fps;

		{ // # init imgui
			ImGui.CreateContext();

			var io = ImGui.GetIO();
			io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
			ImFonts.LoadFonts(io);

			ImGui_Glfw.InitForOpenGL(this.WindowPtr, true);
			ImGui_OpenGL3.Init();
			Console.WriteLine($"OpenGL: {gl.GetString(StringName.Version)}");
			Console.WriteLine($"GLSL: {gl.GetString(StringName.ShadingLanguageVersion)}");

			ImGui.StyleColorsDark();
			var style = ImGui.GetStyle();
			style.ItemInnerSpacing = vec2(8, 4);
			// style.ScaleAllSizes(1.30f);

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
		this.win_preview = new PreviewWindow();
		this.win_palettes = new PalettesWindow();
		this.win_patterns = new PatternsWindow();
		this.win_sequences = new SequencesWindow();
		this.win_clips = new ClipsWindow();
		this.win_fixtures = new FixturesWindow();
		this.win_audio = new AudioWindow();
		this.win_metronome = new MetronomeWindow();
		this.win_macros = new MacrosWindow();
		this.win_push = new PushWindow();

		this.win_funcPlotter = new FuncPlotterWindow {
			Funcs = new() {
				("saw", PatternScript.saw),
				("sine", PatternScript.sine),
				("triangle", PatternScript.triangle),
				("square", PatternScript.square),
			},
		};

		// Push2.Connect();
		// FixtureServer.Start();
	}

	public override void Dispose() {
		AudioIn.Close();
		Push2.Dispose();
		Shaders.Unload();

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

				// # process input
				this.NewInputFrame();
				GLFW.PollEvents();
				GLFW.MakeContextCurrent(this.WindowPtr);

				// # start frame
				ImGui_OpenGL3.NewFrame();
				ImGui_Glfw.NewFrame();
				ImGui.NewFrame();

				// # app stuff
				this.Update(deltaTimeF);
				this.RenderUI();
				// rl.DrawFPS(width - 84, 4);

				// # render
				ImGui.Render();
				gl.Viewport(0, 0, this.ClientSize.X, this.ClientSize.Y);
				glUtil.Clear();
				ImGui_OpenGL3.RenderDrawData(ImGuiNative.igGetDrawData());
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

		this.win_preview.Show();
		this.win_palettes.Show();
		this.win_clips.Show();
		this.win_sequences.Show();
		this.win_patterns.Show();
		this.win_fixtures.Show();
		this.win_audio.Show();
		this.win_metronome.Show();
		this.win_macros.Show();
		// this.funcPlotterWindow.Show();
		this.win_push.Show();
	}
}
