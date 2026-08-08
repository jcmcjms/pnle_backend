using Microsoft.EntityFrameworkCore;
using Pnle.Domain.Auth;
using Pnle.Domain.Tutoring;

namespace Pnle.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<TopicScore> TopicScores => Set<TopicScore>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(x => x.GoogleSubject)
                  .HasMaxLength(128)
                  .IsRequired();

            entity.HasIndex(x => x.GoogleSubject)
                  .IsUnique();

            entity.Property(x => x.Email)
                  .HasMaxLength(320)
                  .IsRequired();

            entity.HasIndex(x => x.Email)
                  .IsUnique();

            entity.Property(x => x.Name)
                  .HasMaxLength(200);

            entity.Property(x => x.PictureUrl)
                  .HasMaxLength(2048);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.Property(x => x.TokenHash)
                  .HasMaxLength(128)
                  .IsRequired();

            entity.HasIndex(x => x.TokenHash)
                  .IsUnique();

            entity.HasOne<User>()
                  .WithMany()
                  .HasForeignKey(x => x.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TopicScore>(entity =>
        {
            entity.Property(x => x.Topic)
                  .HasMaxLength(150)
                  .IsRequired();

            entity.HasIndex(x => new { x.UserId, x.Topic });
        });
    }
}