using System.Runtime.InteropServices;

namespace b_effort.b_led.interop;

static partial class kernel32 {
	const string Lib = "kernel32";

	[LibraryImport(Lib, EntryPoint = "SetThreadAffinityMask", SetLastError = true)]
	public static unsafe partial nint SetThreadAffinityMask(nint hThread, nint dwThreadAffinityMask);

	[LibraryImport(Lib, EntryPoint = "GetCurrentThread")]
	public static unsafe partial nint GetCurrentThread();
}

static partial class winmm {
	const string Lib = "winmm";

	[LibraryImport(Lib, EntryPoint = "timeBeginPeriod")]
	public static unsafe partial uint timeBeginPeriod(uint uPeriod);

	[LibraryImport(Lib, EntryPoint = "timeEndPeriod")]
	public static unsafe partial uint timeEndPeriod(uint uPeriod);
}
