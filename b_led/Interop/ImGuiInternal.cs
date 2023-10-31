using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Runtime.InteropServices.StringMarshalling;

namespace b_effort.b_led.Interop;

static partial class ImGuiInternal {
	const string LibName = "cimgui";

	public const ImGuiSliderFlags ImGuiSliderFlags_Vertical = (ImGuiSliderFlags)(1 << 20);

	[LibraryImport(LibName, EntryPoint = "igDragBehavior", StringMarshalling = Utf8),
	 UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
	[return: MarshalAs(UnmanagedType.I1)]
	public static unsafe partial bool DragBehavior(
		uint id,
		ImGuiDataType data_type,
		ref float v,
		float v_min,
		float v_max,
		string format,
		ImGuiSliderFlags flags
	);
}
