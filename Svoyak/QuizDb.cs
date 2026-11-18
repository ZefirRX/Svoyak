using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace Svoyak;

public class QuizDb(string? databasePath = null) : DbContext
{
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<Question> Questions => Set<Question>();
    private const string DatabaseName = "SQLite_Svoyak.db";

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        var connection = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath ?? FindProjectDatabase(),
            Mode = SqliteOpenMode.ReadWrite
        };
        options.UseSqlite(connection.ToString());
    }

    private static string FindProjectDatabase()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "Svoyak", "Svoyak.csproj")))
                return Path.Combine(folder.FullName, DatabaseName);
        }
        throw new FileNotFoundException($"Запустите приложение из папки проекта с файлом {DatabaseName}.");
    }

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<Game>().ToTable("Game").HasKey(x => x.Id);
        m.Entity<Topic>().ToTable("Topic").HasKey(x => x.Id);
        m.Entity<Question>().ToTable("Question").HasKey(x => x.Id);
        m.Entity<Game>().Property(x => x.CreatedAt).HasDefaultValueSql("strftime('%Y-%m-%dT%H:%M:%SZ', 'now')");
        m.Entity<Game>().Property(x => x.UpdatedAt).HasDefaultValueSql("strftime('%Y-%m-%dT%H:%M:%SZ', 'now')");
        m.Entity<Topic>().HasOne(x => x.Game).WithMany(x => x.Topics).HasForeignKey(x => x.GameId);
        m.Entity<Question>().HasOne(x => x.Topic).WithMany(x => x.Questions).HasForeignKey(x => x.TopicId);
    }
}

