using NAudio.Wave;
using NAudio.Wave.Asio;
using SharpFFTW;

namespace b_effort.b_led;

static class AudioIn {
	const string DeviceName = "ASIO4ALL v2";
	const int SampleRate = 44100;
	
	static AsioOut? asio = null;
	static int numSamples = -1;
	static int numChannels = -1;
	static float[]? inBuffer = null;
	static readonly float[][] channelBuffers = new float[2][];

	public static bool IsOpen => asio != null;
	
	public static string[] GetDeviceNames() => AsioDriver.GetAsioDriverNames();
	
	public static void Open() {
		if (asio != null)
			Close();

		asio = new AsioOut(DeviceName);
		asio.AudioAvailable += OnAsioAudioAvailable;
		asio.InitRecordAndPlayback(null, 2, SampleRate);

		numSamples = asio.FramesPerBuffer;
		numChannels = asio.NumberOfInputChannels;
		inBuffer = new float[numSamples * numChannels];
		channelBuffers[0] = new float[numSamples];
		channelBuffers[1] = new float[numSamples];
		
		asio.Play();
		Console.WriteLine("ASIO open");
	}
	
	public static void Close() {
		if (asio is null)
			return;
		
		asio.AudioAvailable -= OnAsioAudioAvailable;
		asio.Dispose();
		asio = null;
		Console.WriteLine("ASIO closed");
	}

	public static void ShowControlPanel() => asio?.ShowControlPanel();
	
	public delegate void SamplesAvailableHandler(int channel, float[] samples);
	public static event SamplesAvailableHandler? SamplesAvailable;

	static void OnAsioAudioAvailable(object? sender, AsioAudioAvailableEventArgs e) {
		e.GetAsInterleavedSamples(inBuffer);

		for (int ch = 0; ch < channelBuffers.Length; ch++) {
			var buf = channelBuffers[ch];
			for (int i = ch; i < inBuffer!.Length; i += 2) {
				buf[i] = inBuffer[i];
			}
			SamplesAvailable?.Invoke(ch, buf);
		}
	}
}

// it stands for audio analysis, get your mind out of the gutter
static class AudioAnal {
	const int FFT_Size_In = 1024;
	const int FFT_Size_Out = FFT_Size_In / 2 + 1;
	
	static readonly RingBuffer<float> samplesRingBuffer = new(FFT_Size_In);
	
	static readonly float[] fftInBuffer = new float[FFT_Size_In];
	// todo: use System.Numerics.Complex
	static readonly fftw.Complex32[] fftOutBuffer = new fftw.Complex32[FFT_Size_Out];
	static readonly fftw.RealArray fftwIn = new(FFT_Size_In);
	static readonly fftw.ComplexArray fftwOut = new(FFT_Size_Out);
	static readonly fftw.Plan fftwPlan = fftw.Plan.Create1(FFT_Size_In, fftwIn, fftwOut, Options.Estimate);

	static readonly float[] magnitudes = new float[FFT_Size_Out];
	
	static AudioAnal() {
		AudioIn.SamplesAvailable += OnSamplesAvailable;
	}
	
	static void OnSamplesAvailable(int channel, float[] samples) {
		// todo: stereo
		if (channel != 0)
			return;

		samplesRingBuffer.Write(samples);
		samplesRingBuffer.Read(fftInBuffer);
	}

	static void ComputeFFT() {
		fftwIn.Set(fftInBuffer);
		fftwPlan.Execute();
		fftwOut.CopyTo(fftOutBuffer);
		
		for (var i = 0; i < fftOutBuffer.Length; i++) {
			var bin = fftOutBuffer[i];
			magnitudes[i] = bin.Magnitude();
		}
	}

	static float Magnitude(this fftw.Complex32 complex) {
		// from System.Numerics.Complex.Hypot
		float real = Math.Abs(complex.Real);
		float imag = Math.Abs(complex.Imaginary);

		float small, large;
		if (real < imag)
			(small, large) = (real, imag);
		else
			(small, large) = (imag, real);

		if (small == 0.0)
			return large;
		if (float.IsPositiveInfinity(large) && !float.IsNaN(small))
			// The NaN test is necessary so we don't return +inf when small=NaN and large=+inf.
			// NaN in any other place returns NaN without any special handling.
			return float.PositiveInfinity;
		float ratio = small / large;
		return large * MathF.Sqrt(1.0f + ratio * ratio);
	}
}

sealed class RingBuffer<T> {
	readonly int length;
	readonly T[] buffer;
	int writeIndex = 0;

	public int Length => this.length;

	public RingBuffer(int length) {
		this.length = length;
		this.buffer = new T[length];
	}

	public void Write(T[] data) 
	{
		// todo: copy from end if data is larger than buffer
		int copyLength = Math.Min(data.Length, this.length - this.writeIndex);
		Array.Copy(data, 0, this.buffer, this.writeIndex, copyLength);
		this.writeIndex += copyLength;
		if (copyLength < data.Length) {
			int copyLength2 = Math.Min(this.length, data.Length - copyLength);
			Array.Copy(data, copyLength, this.buffer, 0, copyLength2);
			this.writeIndex = copyLength2;
		}
	}

	public void Read(T[] target) {
		if (target.Length != this.length)
			throw new OopsiePoopsie("target buffer should match ring buffer size");
		
		int i = this.writeIndex;
		int copyLength = this.length - i;
		Array.Copy(this.buffer, i, target, 0, copyLength);
		if (copyLength < target.Length) {
			Array.Copy(this.buffer, 0, target, copyLength, this.length - copyLength);
		}
	}
}
