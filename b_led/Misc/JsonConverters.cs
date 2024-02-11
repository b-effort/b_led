namespace System.Text.Json.Serialization;

public sealed class JsonVector2Converter : JsonConverter<Vector2> {
	public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
		if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();

		Vector2 result = new();

		while (reader.Read()) {
			if (reader.TokenType == JsonTokenType.EndObject)
				return result;

			if (reader.TokenType == JsonTokenType.PropertyName) {
				var propName = reader.GetString();
				reader.Read();
				switch (propName) {
					case "x":
						result.X = reader.GetSingle();
						break;
					case "y":
						result.Y = reader.GetSingle();
						break;
				}
			}
		}

		throw new JsonException();
	}

	public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options) {
		writer.WriteStartObject();
		writer.WriteNumber("x", value.X);
		writer.WriteNumber("y", value.Y);
		writer.WriteEndObject();
	}
}
