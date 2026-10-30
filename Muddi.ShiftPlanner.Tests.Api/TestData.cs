using Microsoft.EntityFrameworkCore;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Server.Database.Entities;
using Muddi.ShiftPlanner.Shared.Entities;

namespace Muddi.ShiftPlanner.Tests.Api;

/// <summary>
/// Deterministic schedule used by all API tests:
/// <list type="bullet">
/// <item>current season with location "Hauptbar" (normal) and "Geheim" (AdminOnly type); old season with "Alt"</item>
/// <item>shift types "Theke" (normal, 2 per slot) and "Kasse" (admin-only, 1 per slot); one container with 2 slots</item>
/// <item>slot 1: one shift booked by <see cref="EmployeeId"/>; slot 2: one blocked shift with <see cref="BlockReason"/></item>
/// </list>
/// </summary>
public static class TestData
{
	public static readonly Guid CurrentSeasonId = new("11111111-1111-1111-1111-111111111111");
	public static readonly Guid OldSeasonId = new("11111111-1111-1111-1111-222222222222");
	public static readonly Guid HauptbarId = new("22222222-2222-2222-2222-111111111111");
	public static readonly Guid GeheimId = new("22222222-2222-2222-2222-222222222222");
	public static readonly Guid AltId = new("22222222-2222-2222-2222-333333333333");
	public static readonly Guid HauptbarContainerId = new("33333333-3333-3333-3333-111111111111");
	public static readonly Guid ThekeTypeId = new("44444444-4444-4444-4444-111111111111");
	public static readonly Guid KasseTypeId = new("44444444-4444-4444-4444-222222222222");
	public static readonly Guid EmployeeId = new("55555555-5555-5555-5555-555555555555");
	public static readonly Guid BookedShiftId = new("66666666-6666-6666-6666-111111111111");
	public static readonly Guid BlockedShiftId = new("66666666-6666-6666-6666-222222222222");
	public const string BlockReason = "PRIVATE-REASON";
	public const string Title = "Test Schichtplan";

	public static readonly DateTime Slot1Start = new(2030, 6, 1, 10, 0, 0, DateTimeKind.Utc);
	public static readonly TimeSpan SlotLength = TimeSpan.FromHours(2);
	public static DateTime Slot2Start => Slot1Start + SlotLength;

	public static async Task SeedAsync(ShiftPlannerContext db)
	{
		// A migration already seeds a default season, so key the guard on our own data.
		if (await db.Seasons.AnyAsync(s => s.Id == CurrentSeasonId))
			return;

		var current = new SeasonEntity
		{
			Id = CurrentSeasonId, Name = "Aktuell", IsSelectedAsCurrent = true,
			StartDate = Slot1Start.AddDays(-7), EndDate = Slot1Start.AddDays(7)
		};
		var old = new SeasonEntity
		{
			Id = OldSeasonId, Name = "Alt", IsSelectedAsCurrent = false,
			StartDate = Slot1Start.AddYears(-1).AddDays(-7), EndDate = Slot1Start.AddYears(-1).AddDays(7)
		};
		var normalType = new ShiftLocationTypeEntity { Name = "Bar", AdminOnly = false };
		var secretType = new ShiftLocationTypeEntity { Name = "Intern", AdminOnly = true };
		var theke = new ShiftTypeEntity
			{ Id = ThekeTypeId, Name = "Theke", Color = "#123456", OnlyAssignableByAdmin = false, Season = current };
		var kasse = new ShiftTypeEntity
		{
			Id = KasseTypeId, Name = "Kasse", Color = "#654321", OnlyAssignableByAdmin = true, Season = current,
			Description = "Nur für Admins"
		};
		var framework = new ShiftFrameworkEntity
		{
			Name = "Standard", SecondsPerShift = (int)SlotLength.TotalSeconds, Season = current,
			ShiftTypeCounts =
			[
				new ShiftFrameworkTypeCountEntity { ShiftType = theke, Count = 2 },
				new ShiftFrameworkTypeCountEntity { ShiftType = kasse, Count = 1 }
			]
		};

		var hauptbarContainer = new ShiftContainerEntity
		{
			Id = HauptbarContainerId, Start = Slot1Start, End = Slot1Start + SlotLength * 2, TotalShifts = 2,
			Color = "#ffffff", Framework = framework, Shifts = new List<ShiftEntity>()
		};
		var hauptbar = new ShiftLocationEntity
			{ Id = HauptbarId, Name = "Hauptbar", Type = normalType, Season = current, Containers = [hauptbarContainer] };
		hauptbarContainer.Location = hauptbar;

		var geheimContainer = new ShiftContainerEntity
		{
			Start = Slot1Start, End = Slot1Start + SlotLength * 2, TotalShifts = 2, Color = "#ffffff",
			Framework = framework, Shifts = new List<ShiftEntity>()
		};
		var geheim = new ShiftLocationEntity
			{ Id = GeheimId, Name = "Geheim", Type = secretType, Season = current, Containers = [geheimContainer] };
		geheimContainer.Location = geheim;

		var altFramework = new ShiftFrameworkEntity
		{
			Name = "Alt", SecondsPerShift = (int)SlotLength.TotalSeconds, Season = old,
			ShiftTypeCounts = [new ShiftFrameworkTypeCountEntity { ShiftType = theke, Count = 1 }]
		};
		var altContainer = new ShiftContainerEntity
		{
			Start = Slot1Start.AddYears(-1), End = Slot1Start.AddYears(-1) + SlotLength, TotalShifts = 1,
			Color = "#ffffff", Framework = altFramework, Shifts = new List<ShiftEntity>()
		};
		var alt = new ShiftLocationEntity
			{ Id = AltId, Name = "Alt", Type = normalType, Season = old, Containers = [altContainer] };
		altContainer.Location = alt;

		var booked = new ShiftEntity
		{
			Id = BookedShiftId, EmployeeKeycloakId = EmployeeId, ShiftContainer = hauptbarContainer, Type = theke,
			Start = Slot1Start, End = Slot1Start + SlotLength
		};
		var blocked = new ShiftEntity
		{
			Id = BlockedShiftId, EmployeeKeycloakId = Guid.Empty, BlockReason = BlockReason,
			ShiftContainer = hauptbarContainer, Type = theke, Start = Slot2Start, End = Slot2Start + SlotLength
		};
		hauptbarContainer.Shifts.Add(booked);
		hauptbarContainer.Shifts.Add(blocked);

		db.Seasons.AddRange(current, old);
		db.ShiftLocationTypes.AddRange(normalType, secretType);
		db.ShiftTypes.AddRange(theke, kasse);
		db.ShiftFrameworks.AddRange(framework, altFramework);
		db.ShiftLocations.AddRange(hauptbar, geheim, alt);
		db.Shifts.AddRange(booked, blocked);
		await db.SaveChangesAsync();
	}

	public static async Task SetPublicBrowsingAsync(ShiftPlannerContext db, bool enabled)
	{
		var entity = await db.AppSettings.FirstOrDefaultAsync();
		var settings = new ApplicationSettings
		{
			Title = Title,
			Contact = "kontakt@example.org",
			PublicBrowsingEnabled = enabled
		};
		if (entity is null)
			db.AppSettings.Add(new AppSettingsEntity { Settings = settings });
		else
			entity.Settings = settings;
		await db.SaveChangesAsync();
	}
}
