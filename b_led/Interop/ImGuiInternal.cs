using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Runtime.InteropServices.StringMarshalling;

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
}
