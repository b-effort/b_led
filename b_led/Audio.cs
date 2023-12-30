using NAudio.Wave;
using NAudio.Wave.Asio;

namespace b_effort.b_led;

static class AudioIn {
	const string DeviceName = "ASIO4ALL v2";
	const int SampleRate = 44100;
	
	static AsioOut? asio = null;
	static int numSamples = -1;
	static int numChannels = -1;
	static float[]? inputBuffer = null;
	static readonly float[][] channelBuffers = new float[2][];

	public static bool IsOpen => asio != null;
	
	public static string[] GetDeviceNames() => AsioDriver.GetAsioDriverNames();
	
	public static void Open() {
		if (asio != null)
			Close();

		asio = new AsioOut(DeviceName);
		asio.AudioAvailable += OnAudioAvailable;
		asio.InitRecordAndPlayback(null, 2, SampleRate);

		numSamples = asio.FramesPerBuffer;
		numChannels = asio.NumberOfInputChannels;
		inputBuffer = new float[numSamples * numChannels];
		channelBuffers[0] = new float[numSamples];
		channelBuffers[1] = new float[numSamples];
		
		asio.Play();
		Console.WriteLine("ASIO open");
	}
	
	public static void Close() {
		if (asio is null)
			return;
		
		asio.AudioAvailable -= OnAudioAvailable;
		asio.Dispose();
		asio = null;
		Console.WriteLine("ASIO closed");
	}

	public static void ShowControlPanel() => asio?.ShowControlPanel();

	static void OnAudioAvailable(object? sender, AsioAudioAvailableEventArgs e) {
		e.GetAsInterleavedSamples(inputBuffer);

		for (int ch = 0; ch < channelBuffers.Length; ch++) {
			var buf = channelBuffers[ch];
			for (int i = ch; i < inputBuffer!.Length; i += 2) {
				buf[i] = inputBuffer[i];
			}
		}
	}
}
