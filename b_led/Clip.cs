using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace b_effort.b_led; 

interface ClipContents {
	Guid Id { get; }
	nint? TextureId { get; }
}

[DataContract]
sealed class Clip {
	enum ContentsType {
		Empty,
		Palette,
		Pattern,
		Sequence,
	}
	
	[DataMember] ContentsType contentsType;
	[DataMember] Guid? contentsId;

	ClipContents? contents;
	public ClipContents? Contents {
		[Impl(Inline)] get => this.contents;
		set {
			this.contents = value;
			this.contentsType = this.contents switch {
				null     => ContentsType.Empty,
				Palette  => ContentsType.Palette,
				Pattern  => ContentsType.Pattern,
				Sequence => ContentsType.Sequence,
				_        => throw new ArgumentOutOfRangeException(),
			};
			this.contentsId = value?.Id;
		}
	}

	internal void InitContents(Project project) {
		switch (this.contentsType) {
			case ContentsType.Empty: break;
			case ContentsType.Palette:
				this.Contents = project.Palettes.First(p => p.Id == this.contentsId);
				break;
			case ContentsType.Pattern:
				this.Contents = Pattern.FromId(this.contentsId!.Value);
				break;
			case ContentsType.Sequence:
				this.Contents = project.Sequences.First(s => s.Id == this.contentsId);
				break;
			default: throw new ArgumentOutOfRangeException();
		}
	}
}

[DataContract]
sealed class ClipBank {
	public const int NumCols = 8;
	public const int NumRows = 8;

	[DataMember] public string name;
	[DataMember] public readonly Clip[][] clips;
	
	public ClipBank(string name) {
		this.name = name;
		
		this.clips = new Clip[NumRows][];
		for (var y = 0; y < NumRows; y++) {
			this.clips[y] = new Clip[NumCols];
			for (var x = 0; x < NumCols; x++) {
				this.clips[y][x] = new Clip();
			}
		}
	}

	[JsonConstructor]
	public ClipBank(string name, Clip[][] clips) {
		this.name = name;
		
		int rows = clips.Length;
		int cols = clips[0].Length;
		if (rows != NumRows || cols != NumCols) {
			throw new Exception($"Invalid clips dimensions. rows={rows}, cols={cols}");
		}
		this.clips = clips;
	}

	internal void InitClips(Project project) {
		foreach (var clip in this.clips.SelectMany(x => x)) {
			clip.InitContents(project);
		}
	}
	
	Clip? activePaletteClip;
	Clip? activePatternClip;

	public Palette? ActivePalette => (Palette?)this.activePaletteClip?.Contents;
	public Pattern? ActivePattern => this.activePatternClip?.Contents switch {
		null              => null,
		Pattern pattern   => pattern,
		Sequence sequence => sequence.ActivePattern,
		_                 => throw new ArgumentOutOfRangeException(),
	};

	public bool Activate(Clip clip) {
		switch (clip.Contents) {
			case null: return false;
			case Palette:
				this.activePaletteClip = clip;
				break;
			case Pattern:
			case Sequence:
				this.activePatternClip = clip;
				break;
			default: throw new ArgumentOutOfRangeException();
		}

		return true;
	}

	public bool IsActive(Clip clip) => clip.Contents != null && (
		clip == this.activePaletteClip
	 || clip == this.activePatternClip
	);
}
