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

    public DbSet<TodoItem> TodoItems => Set<TodoItem>();

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

        modelBuilder.Entity<TodoItem>(entity =>
        {
            entity.ToTable("TodoItems");
            entity.HasKey(todo => todo.Id);

            entity.Property(todo => todo.Id)
                .HasColumnType("TEXT")
                .IsRequired();

            entity.Property(todo => todo.Date)
                .HasColumnType("TEXT")
                .IsRequired();

            entity.Property(todo => todo.Text)
                .HasColumnType("TEXT")
                .IsRequired();

            entity.Property(todo => todo.StartTime)
                .HasColumnType("TEXT");

            entity.Property(todo => todo.EndTime)
                .HasColumnType("TEXT");

            entity.Property(todo => todo.IsCompleted)
                .HasColumnType("INTEGER")
                .HasDefaultValue(false)
                .IsRequired();

            entity.Property(todo => todo.CreatedAt)
                .HasColumnType("TEXT")
                .IsRequired();

            entity.Property(todo => todo.UpdatedAt)
                .HasColumnType("TEXT")
                .IsRequired();

            entity.Property(todo => todo.CompletedAt)
                .HasColumnType("TEXT");

            entity.Property(todo => todo.IsDeleted)
                .HasColumnType("INTEGER")
                .HasDefaultValue(false)
                .IsRequired();

            entity.HasIndex(todo => new { todo.Date, todo.IsDeleted });
        });
    }
}
