using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;
using NodaTime.Text;

namespace MountainManager.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskPriority> TaskPriorities => Set<TaskPriority>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var localDateConverter = new ValueConverter<LocalDate, string>(
            value => LocalDatePattern.Iso.Format(value),
            value => LocalDatePattern.Iso.Parse(value).Value);

        var instantConverter = new ValueConverter<Instant, DateTime>(
            value => value.ToDateTimeUtc(),
            value => Instant.FromDateTimeUtc(DateTime.SpecifyKind(value, DateTimeKind.Utc)));

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Email).HasMaxLength(320).IsRequired();
            entity.HasIndex(user => user.Email).IsUnique();
            entity.Property(user => user.PasswordHash).IsRequired();
            entity.Property(user => user.CreatedAt).HasConversion(instantConverter);
        });

        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.HasKey(task => task.Id);
            entity.Property(task => task.Title).HasMaxLength(120).IsRequired();
            entity.Property(task => task.Description).HasMaxLength(2000);
            entity.Property(task => task.DueDate).HasConversion(localDateConverter).HasMaxLength(10);
            entity.Property(task => task.CreatedAt).HasConversion(instantConverter);
            entity.Property(task => task.UpdatedAt).HasConversion(instantConverter);
            entity.Property(task => task.CompletedAt).HasConversion(instantConverter);
            entity.HasIndex(task => new { task.UserId, task.DueDate, task.PriorityId });
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(task => task.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(task => task.Priority)
                .WithMany()
                .HasForeignKey(task => task.PriorityId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TaskPriority>(entity =>
        {
            entity.HasKey(priority => priority.Id);
            entity.Property(priority => priority.Name).HasMaxLength(16).IsRequired();
            entity.Property(priority => priority.SortRank).IsRequired();
            entity.HasIndex(priority => priority.Name).IsUnique();
            entity.HasData(
                new TaskPriority { Id = TaskPriority.LowId, Name = "Low", SortRank = 1 },
                new TaskPriority { Id = TaskPriority.MediumId, Name = "Medium", SortRank = 2 },
                new TaskPriority { Id = TaskPriority.HighId, Name = "High", SortRank = 3 },
                new TaskPriority { Id = TaskPriority.UrgentId, Name = "Urgent", SortRank = 4 });
        });
    }
}
