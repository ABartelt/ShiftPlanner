using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Muddi.ShiftPlanner.Server.Database.Entities;

namespace Muddi.ShiftPlanner.Server.Database.Contexts;

public class ShiftPlannerContext : DbContext, IDataProtectionKeyContext
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
	public DbSet<PromotionChannelEntity> PromotionChannels { get; set; }
	public DbSet<PromotionScheduleEntity> PromotionSchedules { get; set; }
	public DbSet<PromotionLogEntity> PromotionLogs { get; set; }

	/// <summary>
	/// Key ring of ASP.NET Core Data Protection, which encrypts the promotion channel secrets.
	/// </summary>
	public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

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

		// Promotion data belongs to a season (event) and is removed together with it.
		modelBuilder.Entity<PromotionChannelEntity>(entity =>
		{
			entity.HasOne(e => e.Season).WithMany().OnDelete(DeleteBehavior.Cascade);
			entity.Property(e => e.Kind).HasConversion<string>().HasMaxLength(20);
			entity.OwnsOne(e => e.Config, owned => owned.ToJson());
			entity.Navigation(e => e.Config).IsRequired();
		});
		modelBuilder.Entity<PromotionScheduleEntity>(entity =>
		{
			entity.HasOne(e => e.Season).WithMany().OnDelete(DeleteBehavior.Cascade);
			entity.HasIndex("SeasonId").IsUnique();
			entity.OwnsOne(e => e.Settings, owned => owned.ToJson());
			entity.Navigation(e => e.Settings).IsRequired();
		});
		modelBuilder.Entity<PromotionLogEntity>(entity =>
		{
			entity.HasOne(e => e.Season).WithMany().OnDelete(DeleteBehavior.Cascade);
			entity.HasOne(e => e.Channel).WithMany().OnDelete(DeleteBehavior.SetNull);
			entity.Property(e => e.RunKind).HasConversion<string>().HasMaxLength(20);
			entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
			entity.HasIndex(e => e.RunKey);
		});
	}
}