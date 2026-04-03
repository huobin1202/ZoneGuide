using Microsoft.Data.Sqlite;

var dbPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ZoneGuide.mobile.db"));
if (!File.Exists(dbPath))
{
	Console.WriteLine($"DB not found: {dbPath}");
	return;
}

using var conn = new SqliteConnection($"Data Source={dbPath}");
conn.Open();

Console.WriteLine($"DB: {dbPath}");
Console.WriteLine();

PrintQuery(conn, "Tables", "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;");
PrintQuery(conn, "POI count", "SELECT COUNT(*) AS Count FROM POI;");
PrintQuery(conn, "POITranslation count", "SELECT COUNT(*) AS Count FROM POITranslation;");
PrintQuery(conn, "POI samples", "SELECT Id, Name, Language, substr(TTSScript,1,80) AS TTSScript FROM POI ORDER BY Id LIMIT 10;");
PrintQuery(conn, "POITranslation samples", "SELECT POIId, LanguageCode, Name, substr(TTSScript,1,80) AS TTSScript, length(ShortDescription) AS ShortLen, length(FullDescription) AS FullLen FROM POITranslation ORDER BY POIId, LanguageCode LIMIT 30;");

static void PrintQuery(SqliteConnection conn, string title, string sql)
{
	Console.WriteLine($"=== {title} ===");
	using var cmd = conn.CreateCommand();
	cmd.CommandText = sql;
	using var reader = cmd.ExecuteReader();

	if (!reader.HasRows)
	{
		Console.WriteLine("(no rows)");
		Console.WriteLine();
		return;
	}

	var cols = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
	Console.WriteLine(string.Join(" | ", cols));

	while (reader.Read())
	{
		var values = new string[reader.FieldCount];
		for (var i = 0; i < reader.FieldCount; i++)
		{
			values[i] = reader.IsDBNull(i) ? "NULL" : (reader.GetValue(i)?.ToString() ?? string.Empty);
		}

		Console.WriteLine(string.Join(" | ", values));
	}

	Console.WriteLine();
}
