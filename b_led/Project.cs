using System.IO;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace b_effort.b_led;

[DataContract]
sealed class Project {
	static readonly JsonSerializerOptions serializerOptions = new() {
		TypeInfoResolver = new DataContractJsonTypeInfoResolver {
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		},
		WriteIndented = true,
	};
	
	public const string FileExt = "blep";
	
	[DataMember] public List<Palette> Palettes { get; set; } = new();
	[DataMember] public List<Sequence> Sequences { get; set; } = new();
	[DataMember] public ClipBank[] ClipBanks { get; set; } = new ClipBank[8] {
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
