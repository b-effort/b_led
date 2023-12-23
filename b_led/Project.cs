using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace b_effort.b_led;

[JsonSourceGenerationOptions(
	GenerationMode = JsonSourceGenerationMode.Metadata,
	IgnoreReadOnlyProperties = true,
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	WriteIndented = true
)]
[JsonSerializable(typeof(Project))]
sealed partial class ProjectSerializerContext : JsonSerializerContext { }

sealed class Project {
	public const string FileExt = "blep";
	
	public List<Palette> Palettes { get; init; } = new();
	public ClipBank[] ClipBanks { get; init; } = new ClipBank[8];

	public static Project Load(string filePath) {
		string json = File.ReadAllText(filePath);
		Project? project = JsonSerializer.Deserialize(json, ProjectSerializerContext.Default.Project);
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
		string json = JsonSerializer.Serialize(this, ProjectSerializerContext.Default.Project);
		File.WriteAllText(filePath, json);
		Console.WriteLine($"Project saved: {filePath}");
	}
}
