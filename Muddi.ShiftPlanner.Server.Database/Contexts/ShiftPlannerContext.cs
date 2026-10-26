using Microsoft.EntityFrameworkCore;
using Muddi.ShiftPlanner.Server.Database.Entities;

namespace Muddi.ShiftPlanner.Server.Database.Contexts;

public class ShiftPlannerContext : DbContext
{
	public ShiftPlannerContext(DbContextOptions<ShiftPlannerContext> options) : base(options)
	{
	}

	public DbSet<SeasonEntity> Seasons { get; set; }
	public DbSet<ShiftEntity> Shifts { get; set; }
	public DbSet<ShiftContainerEntity> Containers { get; set; }
	public DbSet<ShiftFrameworkEntity> ShiftFrameworks { get; set; }
	public DbSet<ShiftTypeEntity> ShiftTypes { get; set; }
	public DbSet<ShiftLocationEntity> ShiftLocations { get; set; }
	public DbSet<ShiftLocationTypeEntity> ShiftLocationTypes { get; set; }
	public DbSet<AppSettingsEntity> AppSettings { get; set; }
	public DbSet<NotificationPreferencesEntity> NotificationPreferences { get; set; }
	public DbSet<NotificationChannelLinkEntity> NotificationChannelLinks { get; set; }
	public DbSet<NotificationLinkCodeEntity> NotificationLinkCodes { get; set; }
	public DbSet<ShiftNotificationEntity> ShiftNotifications { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		// Single-row table; the whole settings object lives in one jsonb column (EF Core JSON mapping).
		modelBuilder.Entity<AppSettingsEntity>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id).ValueGeneratedNever();
			entity.OwnsOne(e => e.Settings, owned => owned.ToJson());
			entity.Navigation(e => e.Settings).IsRequired();
		});

		modelBuilder.Entity<NotificationPreferencesEntity>(entity =>
		{
			entity.Property(e => e.EmployeeKeycloakId).ValueGeneratedNever();
		});

		// Several people may share one phone, so the address is not unique
		modelBuilder.Entity<NotificationChannelLinkEntity>(entity =>
		{
			entity.Property(e => e.Channel).HasConversion<string>().HasMaxLength(16);
			entity.HasIndex(e => new { e.EmployeeKeycloakId, e.Channel }).IsUnique();
			entity.HasIndex(e => new { e.Channel, e.Address });
			entity.HasIndex(e => e.UnsubscribeToken).IsUnique();
		});

		modelBuilder.Entity<NotificationLinkCodeEntity>(entity =>
		{
			entity.Property(e => e.Channel).HasConversion<string>().HasMaxLength(16);
		});

		modelBuilder.Entity<ShiftNotificationEntity>(entity =>
		{
			entity.Property(e => e.Channel).HasConversion<string>().HasMaxLength(16);
			entity.Property(e => e.Kind).HasConversion<string>().HasMaxLength(16);
			entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(16);
			entity.HasOne(e => e.Shift)
				.WithMany()
				.HasForeignKey(e => e.ShiftId)
				.OnDelete(DeleteBehavior.SetNull);
			entity.HasIndex(e => new { e.ShiftId, e.EmployeeKeycloakId, e.Kind, e.ShiftStart, e.Channel }).IsUnique();
			entity.HasIndex(e => e.ActionToken).IsUnique();
			entity.HasIndex(e => new { e.Channel, e.Address });
			entity.HasIndex(e => e.CreatedAt);
		});
	}
}