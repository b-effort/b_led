namespace System.Text.Json;

sealed class JsonSnakeCaseNamingPolicy : JsonNamingPolicy {
	static JsonSnakeCaseNamingPolicy? @default;
	public static JsonSnakeCaseNamingPolicy Default => @default ??= new();
	
	const char Separator = '_';

	public override string ConvertName(string name) {
		if (string.IsNullOrWhiteSpace(name))
			return string.Empty;

		ReadOnlySpan<char> spanName = name.Trim();

		var sb = new StringBuilder();
		var addCharacter = true;

		bool next_lower = false;
		bool next_upper = false;
		bool next_space = false;

		for (int i = 0; i < spanName.Length; i++) {
			if (i != 0) {
				bool curr_space = spanName[i] == 32;
				bool prev_space = spanName[i - 1] == 32;
				bool prev_separator = spanName[i - 1] == 95;

				if (i + 1 != spanName.Length) {
					next_lower = spanName[i + 1] > 96 && spanName[i + 1] < 123;
					next_upper = spanName[i + 1] > 64 && spanName[i + 1] < 91;
					next_space = spanName[i + 1] == 32;
				}

				if (
					curr_space && (
						prev_space
					 || prev_separator
					 || next_upper
					 || next_space
					)
				) {
					addCharacter = false;
				} else {
					bool curr_upper = spanName[i] > 64 && spanName[i] < 91;
					bool prev_lower = spanName[i - 1] > 96 && spanName[i - 1] < 123;
					bool prev_number = spanName[i - 1] > 47 && spanName[i - 1] < 58;

					if (
						curr_upper && (
							prev_lower
						 || prev_number
						 || next_lower
						 || next_space
						 || (next_lower && !prev_space)
						)
					) {
						sb.Append(Separator);
					} else if (curr_space && !prev_space && !next_space) {
						sb.Append(Separator);
						addCharacter = false;
					}
				}
			}

			if (addCharacter)
				sb.Append(spanName[i]);
			else
				addCharacter = true;
		}

		return sb.ToString().ToLower();
	}
}
