using System.IO;

namespace Svoyak;

internal static class AppData
{
    private const string DatabaseName = "SQLite_Svoyak.db";
    private static readonly string DataDirectory = AppContext.BaseDirectory;

#if SINGLE_FILE
    private static readonly string ExecutableName = Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "Svoyak";
    public static string TimersPath => Path.Combine(DataDirectory, $"{ExecutableName}.timers.json");

    public static string GetDatabasePath()
    {
        var databasePath = Path.Combine(DataDirectory, $"{ExecutableName}.db");
        if (File.Exists(databasePath))
            return databasePath;

        var temporaryPath = Path.Combine(DataDirectory, $"{Guid.NewGuid():N}.tmp");
        try
        {
            using var seed = typeof(AppData).Assembly.GetManifestResourceStream($"Svoyak.{DatabaseName}")
                ?? throw new InvalidOperationException("Встроенная база игр отсутствует.");
            using (var destination = File.Create(temporaryPath))
                seed.CopyTo(destination);

            try
            {
                File.Move(temporaryPath, databasePath);
            }
            catch (IOException) when (File.Exists(databasePath))
            {
                // Another instance finished initializing the database first.
            }
        }
        finally
        {
            File.Delete(temporaryPath);
        }

        return databasePath;
    }
#else
    public static string TimersPath => Path.Combine(DataDirectory, "timers.json");

    public static string GetDatabasePath()
    {
        for (var folder = new DirectoryInfo(DataDirectory); folder != null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "Svoyak", "Svoyak.csproj")))
                return Path.Combine(folder.FullName, DatabaseName);
        }

        throw new FileNotFoundException($"Запустите приложение из папки проекта с файлом {DatabaseName}.");
    }
#endif
}
