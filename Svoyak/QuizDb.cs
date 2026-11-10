using Microsoft.EntityFrameworkCore;
using System.IO;

namespace Svoyak;

public class QuizDb : DbContext
{
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<Question> Questions => Set<Question>();
    protected override void OnConfiguring(DbContextOptionsBuilder b) => b.UseSqlite($"Data Source={Path.Combine(AppContext.BaseDirectory, "SQLite_Svoyak.db")}");
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

