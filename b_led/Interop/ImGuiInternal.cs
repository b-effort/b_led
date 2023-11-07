using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Runtime.InteropServices.StringMarshalling;

[assembly:DisableRuntimeMarshalling]

namespace b_effort.b_led.Interop;

static partial class ImGuiInternal {
	const string LibName = "cimgui";

	public const ImGuiSliderFlags ImGuiSliderFlags_Vertical = (ImGuiSliderFlags)(1 << 20);

	[LibraryImport(LibName, EntryPoint = "igDragBehavior", StringMarshalling = Utf8),
	 UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
	[return: MarshalAs(UnmanagedType.U1)]
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

	[LibraryImport(LibName, EntryPoint = "igRenderFrameBorder"),
	 UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
	public static unsafe partial void RenderFrameBorder(Vector2 p_min, Vector2 p_max, float rounding);

	[LibraryImport(LibName, EntryPoint = "igRenderArrowPointingAt"),
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
