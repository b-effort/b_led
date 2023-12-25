using System.IO;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace b_effort.b_led;

[DataContract]
sealed class Project {
	static readonly JsonNamingPolicy namingPolicy = JsonSnakeCaseNamingPolicy.Default;
	static readonly JsonSerializerOptions serializerOptions = new() {
		Converters = {
			new JsonStringEnumConverter(namingPolicy),
		},
		TypeInfoResolver = new DataContractJsonTypeInfoResolver {
			PropertyNamingPolicy = namingPolicy,
		},
		WriteIndented = true,
	};

	public const string FileExt = "blep";

	[DataMember] public List<Palette> Palettes { get; private init; } = new();
	[DataMember] public List<Sequence> Sequences { get; private init; } = new() {
		new(),
	};
	[DataMember] public ClipBank[] ClipBanks { get; private init; } = new ClipBank[8] {
		new("bank 1"),
		new("bank 2"),
		new("bank 3"),
		new("bank 4"),
		new("bank 5"),
		new("bank 6"),
		new("bank 7"),
		new("bank 8"),
	};

	public static Project Load(string filePath) {
		string json = File.ReadAllText(filePath);
		Project? project = JsonSerializer.Deserialize<Project>(json, serializerOptions);
		if (project is null) {
			throw new Exception($"Failed to load project: {filePath}");
		}

		foreach (ClipBank bank in project.ClipBanks)
		foreach (Clip[] clips in bank.clips)
		foreach (Clip clip in clips)
			clip.LoadContents(project);

		Console.WriteLine($"Project loaded: {filePath}");
		return project;
	}

	public void Save(string filePath) {
		string json = JsonSerializer.Serialize(this, serializerOptions);
		File.WriteAllText(filePath, json);
		Console.WriteLine($"Project saved: {filePath}");
	}
}
