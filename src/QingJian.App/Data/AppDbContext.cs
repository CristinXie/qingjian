using Microsoft.EntityFrameworkCore;
using QingJian.App.Models;

namespace QingJian.App.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Note>(entity =>
        {
            entity.ToTable("Notes");
            entity.HasKey(note => note.Id);

            entity.Property(note => note.Id)
                .HasColumnType("TEXT")
                .IsRequired();

            entity.Property(note => note.Title)
                .HasColumnType("TEXT")
                .IsRequired();

            entity.Property(note => note.Content)
                .HasColumnType("TEXT")
                .IsRequired();

            entity.Property(note => note.CreatedAt)
                .HasColumnType("TEXT")
                .IsRequired();

            entity.Property(note => note.UpdatedAt)
                .HasColumnType("TEXT")
                .IsRequired();

            entity.Property(note => note.IsDeleted)
                .HasColumnType("INTEGER")
                .HasDefaultValue(false)
                .IsRequired();
        });
    }
}
