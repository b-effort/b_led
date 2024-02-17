using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using b_effort.b_led.graphics;
using JetBrains.Annotations;

namespace b_effort.b_led;

static class PatternScript {
	public static float t { [Impl(Inline)] get => Metronome.TSynced; }
	public static float dt => Metronome.TDelta;
	public static float interval(float beats) => Metronome.SyncedInterval(beats);

	public static float saw(float x) => x % 1f;
	public static float sine(float x) => BMath.sin01(x - 0.25f);
	public static float triangle(float x) {
		float x2 = x % 1 * 2;
		return x2 <= 1f ? x2 : 2f - x2;
	}
	public static float square(float x) => pulse(x + 0.5f, 0.5f);
	public static float pulse(float x, float dutyCycle) => x % 1 >= 1 - (dutyCycle % 1) ? 1f : 0f;

	// todo: harmonic series

	public static class beat {
		public static float saw(float beats = 1f, float phase = 0f) => PatternScript.saw(interval(beats) + phase);
		public static float sine(float beats, float phase = 0f) => PatternScript.sine(interval(beats) + phase);
		public static float triangle(float beats, float phase = 0f) => PatternScript.triangle(interval(beats) + phase);
		public static float square(float beats, float phase = 0f) => PatternScript.square(interval(beats) + phase);
		public static float pulse(float beats, float dutyCycle, float phase = 0f) => PatternScript.pulse(interval(beats) + phase, dutyCycle);
	}
}

sealed class Macro {
	float value;
	public float Value {
		[Impl(Inline)] get => this.value;
		set => this.value = BMath.clamp(value, this.Min, this.Max);
	}

	public required string Name { get; init; }
	public float Min { get; init; } = 0f;
	public float Max { get; init; } = 1f;
	public float Range => this.Max - this.Min;

	public static implicit operator float(Macro @this) => @this.value;

	public static readonly Macro scaleX = new() { Name = "scale x", Value = 1f, Min = 0.1f, Max = 10f };
	public static readonly Macro scaleY = new() { Name = "scale y", Value = 1f, Min = 0.1f, Max = 10f };
	public static readonly Macro hue_offset = new() { Name = "hue", Min = -5f, Max = 5f };

	static Macro[]? global;
	public static IReadOnlyList<Macro> Global => global
		??= new[] { scaleX, scaleY, hue_offset };
}

[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature, ImplicitUseTargetFlags.WithInheritors)]
abstract class Pattern : ClipContents, IDisposable {
	public static readonly Pattern[] All = AppDomain.CurrentDomain.GetAssemblies()
		.SelectMany(a => a.GetTypes())
		.Where(t => t.IsSealed && typeof(Pattern).IsAssignableFrom(t))
		.Select(t => (Pattern)Activator.CreateInstance(t)!)
		.ToArray();

	public static Pattern FromId(Guid id) => All.First(p => p.Id == id);

	public Guid Id { get; }

	public readonly string name;

	public Macro m1 = new() { Name = "macro 1" };
	public Macro m2 = new() { Name = "macro 2" };
	public Macro m3 = new() { Name = "macro 3" };
	public Macro m4 = new() { Name = "macro 4" };

	Macro[]? macros;
	public IReadOnlyList<Macro> Macros => this.macros
		??= new[] { this.m1, this.m2, this.m3, this.m4 };

	static int PreviewWidth => (int)Config.PatternPreviewResolution.X;
	static int PreviewHeight => (int)Config.PatternPreviewResolution.Y;

	readonly Texture2D previewTexture;
	public nint TextureId => this.previewTexture.id;
	nint? ClipContents.TextureId => this.TextureId;

	protected Pattern(Guid id) {
		this.Id = id;
		this.name = this.GetDerivedNameFromType();
		this.previewTexture = new Texture2D(PreviewWidth, PreviewHeight);
	}

	~Pattern() => this.Dispose();

	public void Dispose() {
		this.previewTexture.Dispose();
		GC.SuppressFinalize(this);
	}

	public void UpdatePreview() {
		float scaleX = Macro.scaleX;
		float scaleY = Macro.scaleY;

		RGBA[] pixels = this.previewTexture.pixels;
		float widthMinusOne = PreviewWidth - 1f;
		for (int y = 0; y < PreviewHeight; y++)
		for (int x = 0; x < PreviewWidth; x++) {
			int i = y * PreviewWidth + x;
			float x01 = x / widthMinusOne * scaleX;
			float y01 = y / widthMinusOne * scaleY;

			HSB pixel = this.Render(i, x01, y01);
			pixels[i] = pixel.ToRGBA();
		}

		this.previewTexture.Update();
	}

	public void RenderTo(RGBA[] leds, Vector2[] coords, Vector2 bounds, Palette? palette) {
		float scaleX = Macro.scaleX;
		float scaleY = Macro.scaleY;
		float hueOffset = Macro.hue_offset;

		for (var i = 0; i < leds.Length; i++) {
			Vector2 pos = coords[i] / bounds;

			HSB color = this.Render(i, pos.X * scaleX, pos.Y * scaleY);
			// !todo: handle negative hue offset
			color.h += hueOffset;
			if (color.h > 1f)
				color.h %= 1f;
			if (palette != null)
				color = palette.gradient.MapColor(color);

			leds[i] = color.ToRGBA();
		}
	}

	public virtual void Tick() { }
	protected abstract HSB Render(int i, float x, float y);
}

[DataContract]
sealed class Sequence : ClipContents {
	[DataContract]
	public sealed class Slot {
		public Pattern? pattern;
		[DataMember] public Guid? PatternId => this.pattern?.Id;

		[JsonConstructor]
		public Slot(Guid? pattern_id) {
			if (pattern_id.HasValue)
				this.pattern = Pattern.FromId(pattern_id.Value);
		}

		public Slot() : this(pattern_id: null) { }

		public bool HasPattern => this.pattern != null;
	}

	public const int SlotsMin = 2;
	public const int SlotsMax = 16;
	public const int Name_MaxLength = 64;

	[DataMember] public Guid Id { get; }
	[DataMember] public string name;
	[DataMember] public TimeFraction slotDuration;

	[DataMember] readonly List<Slot> slots;
	public IReadOnlyList<Slot> Slots => this.slots;

	[JsonConstructor]
	public Sequence(Guid id, string name, List<Slot> slots, TimeFraction slot_duration) {
		this.Id = id;
		this.name = name;
		this.slots = slots;
		this.slotDuration = slot_duration;
	}

	public Sequence(string name = "new sequence") : this(
		id: Guid.NewGuid(),
		name,
		slots: Enumerable.Range(0, 8).Select(_ => new Slot()).ToList(),
		slot_duration: new TimeFraction(1, 4)
	) { }

	public string Label => $"{this.name} ({this.Slots.Count})";

	public Slot? ActiveSlot {
		get {
			if (this.Slots.Count == 0)
				return null;
			float interval = Metronome.SyncedInterval(this.slots.Count);
			var i = (int)(interval * this.slots.Count);
			while (i > 0 && !this.slots[i].HasPattern) {
				i--;
			}
			return this.slots[i];
		}
	}

	public Pattern? ActivePattern => this.ActiveSlot?.pattern;
	public nint? TextureId => this.ActivePattern?.TextureId;

	public void Add() {
		this.slots.Add(new Slot());
	}

	public bool RemoveAt(int i) {
		if (i < 0 || this.slots.Count <= i)
			return false;

		this.slots.RemoveAt(i);
		return true;
	}

	public void Resize(int numSlots) {
		if (numSlots is < SlotsMin or > SlotsMax)
			throw new OopsiePoopsie("invalid number of slots");

		var slots = this.slots;
		if (numSlots < slots.Count) {
			slots.RemoveRange(numSlots, slots.Count - numSlots);
		} else {
			while (slots.Count < numSlots) {
				slots.Add(new());
			}
		}
	}
}
