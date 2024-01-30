using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using GLFW_Window = OpenTK.Windowing.GraphicsLibraryFramework.Window;
using static System.Runtime.InteropServices.StringMarshalling;
using static System.Runtime.InteropServices.UnmanagedType;

[assembly:DisableRuntimeMarshalling]

namespace b_effort.b_led.interop;

static partial class ImGuiEx {
	const string Lib = "cimgui";

	public const ImGuiSliderFlags ImGuiSliderFlags_Vertical = (ImGuiSliderFlags)(1 << 20);

	[LibraryImport(Lib, EntryPoint = "igDragBehavior", StringMarshalling = Utf8),
	 UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
	[return: MarshalAs(U1)]
	public static unsafe partial bool DragBehavior(
		uint id,
		ImGuiDataType data_type,
		nint v,
		float speed,
		nint v_min,
		nint v_max,
		string format,
		ImGuiSliderFlags flags
	);

	[LibraryImport(Lib, EntryPoint = "igRenderFrame"),
	 UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
	public static unsafe partial void RenderFrame(
		Vector2 p_min,
		Vector2 p_max,
		uint col,
		[MarshalAs(U1)] bool border,
		float rounding
	);

	public static void RenderFrame(Vector2 p_min, Vector2 p_max, uint col) =>
		RenderFrame(p_min, p_max, col, false, 0);

	[LibraryImport(Lib, EntryPoint = "igRenderFrameBorder"),
	 UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
	public static unsafe partial void RenderFrameBorder(Vector2 p_min, Vector2 p_max, float rounding);

	[LibraryImport(Lib, EntryPoint = "igRenderArrowPointingAt"),
	 UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
	private static unsafe partial void RenderArrowPointingAt(
		ImDrawList* draw_list,
		Vector2 pos,
		Vector2 half_sz,
		ImGuiDir direction,
		uint col
	);

	public static unsafe void RenderArrowPointingAt(
		ImDrawListPtr drawList,
		Vector2 pos,
		Vector2 halfSize,
		ImGuiDir dir,
		uint color
	) => RenderArrowPointingAt(drawList.NativePtr, pos, halfSize, dir, color);
}

static partial class ImGui_Glfw {
	const string Lib = "cimgui";

	[LibraryImport(Lib, EntryPoint = "ImGui_ImplGlfw_InitForOpenGL"),
	 UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
	[return: MarshalAs(U1)]
	public static unsafe partial bool InitForOpenGL(
		GLFW_Window* window,
		[MarshalAs(U1)] bool install_callbacks
	);

	[LibraryImport(Lib, EntryPoint = "ImGui_ImplGlfw_NewFrame"),
	 UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
	public static unsafe partial void NewFrame();

	[LibraryImport(Lib, EntryPoint = "ImGui_ImplGlfw_Shutdown"),
	 UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
	public static unsafe partial void Shutdown();
}

static partial class ImGui_OpenGL3 {
	const string Lib = "cimgui";

	[LibraryImport(Lib, EntryPoint = "ImGui_ImplOpenGL3_Init", StringMarshalling = Utf8),
	 UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
	[return: MarshalAs(U1)]
	public static unsafe partial bool Init(string? glsl_version = null);

	[LibraryImport(Lib, EntryPoint = "ImGui_ImplOpenGL3_NewFrame"),
	 UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
	public static unsafe partial void NewFrame();

	[LibraryImport(Lib, EntryPoint = "ImGui_ImplOpenGL3_RenderDrawData"),
	 UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
	public static unsafe partial void RenderDrawData(ImDrawData* draw_data);

	[LibraryImport(Lib, EntryPoint = "ImGui_ImplOpenGL3_Shutdown"),
	 UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
	public static unsafe partial void Shutdown();
}
