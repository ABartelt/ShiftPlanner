using Mapster;
using Microsoft.EntityFrameworkCore;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Server.Database.Entities;
using Muddi.ShiftPlanner.Shared.Contracts.v1.Responses.Seasons;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Seasons;

public class CreateEndpoint : CrudCreateEndpoint<CreateSeasonRequest, GetSeasonResponse, GetEndpoint>
{
	public CreateEndpoint(ShiftPlannerContext database) : base(database)
	{
	}

	protected override void CrudConfigure()
	{
		Post("/seasons");
	}

	public override async Task<GetSeasonResponse?> CrudExecuteAsync(CreateSeasonRequest req, CancellationToken ct)
	{
		var season = new SeasonEntity
		{
			Name = req.Name,
			StartDate = req.StartDate,
			EndDate = req.EndDate
		};

		Database.Seasons.Add(season);

		if (req.CopyFromSeasonId.HasValue)
		{
			await CopyFromSeasonAsync(req.CopyFromSeasonId.Value, season, ct);
		}

		await Database.SaveChangesAsync(ct);
		return season.Adapt<GetSeasonResponse>();
	}

	private async Task CopyFromSeasonAsync(Guid sourceSeasonId, SeasonEntity targetSeason, CancellationToken ct)
	{
		var sourceSeason = await Database.Seasons.FindAsync([sourceSeasonId], ct);
		if (sourceSeason is null)
		{
			ThrowError("Source season not found", 404);
		}

		// Calculate date offset for containers
		var dateOffset = targetSeason.StartDate - sourceSeason.StartDate;

		// 1. Copy ShiftTypes (need mapping for Framework references)
		var sourceShiftTypes = await Database.ShiftTypes
			.Where(st => st.Season.Id == sourceSeasonId)
			.ToListAsync(ct);

		var shiftTypeMapping = new Dictionary<Guid, ShiftTypeEntity>();
		foreach (var sourceType in sourceShiftTypes)
		{
			var newType = new ShiftTypeEntity
			{
				Id = Guid.NewGuid(),
				Name = sourceType.Name,
				Color = sourceType.Color,
				StartingTimeShift = sourceType.StartingTimeShift,
				OnlyAssignableByAdmin = sourceType.OnlyAssignableByAdmin,
				Description = sourceType.Description,
				Season = targetSeason
			};
			Database.ShiftTypes.Add(newType);
			shiftTypeMapping[sourceType.Id] = newType;
		}

		// 2. Copy Frameworks with ShiftTypeCounts
		var sourceFrameworks = await Database.ShiftFrameworks
			.Include(f => f.ShiftTypeCounts)
			.ThenInclude(stc => stc.ShiftType)
			.Where(f => f.Season.Id == sourceSeasonId)
			.ToListAsync(ct);

		var frameworkMapping = new Dictionary<Guid, ShiftFrameworkEntity>();
		foreach (var sourceFramework in sourceFrameworks)
		{
			var newFramework = new ShiftFrameworkEntity
			{
				Id = Guid.NewGuid(),
				Name = sourceFramework.Name,
				SecondsPerShift = sourceFramework.SecondsPerShift,
				Season = targetSeason,
				ShiftTypeCounts = new List<ShiftFrameworkTypeCountEntity>()
			};

			// Copy ShiftTypeCounts with mapped ShiftTypes
			foreach (var sourceCount in sourceFramework.ShiftTypeCounts)
			{
				if (shiftTypeMapping.TryGetValue(sourceCount.ShiftType.Id, out var newShiftType))
				{
					newFramework.ShiftTypeCounts.Add(new ShiftFrameworkTypeCountEntity
					{
						Id = Guid.NewGuid(),
						ShiftFramework = newFramework,
						ShiftType = newShiftType,
						Count = sourceCount.Count
					});
				}
			}

			Database.ShiftFrameworks.Add(newFramework);
			frameworkMapping[sourceFramework.Id] = newFramework;
		}

		// 3. Copy Locations
		var sourceLocations = await Database.ShiftLocations
			.Include(l => l.Type)
			.Where(l => l.Season.Id == sourceSeasonId)
			.ToListAsync(ct);

		var locationMapping = new Dictionary<Guid, ShiftLocationEntity>();
		foreach (var sourceLocation in sourceLocations)
		{
			var newLocation = new ShiftLocationEntity
			{
				Id = Guid.NewGuid(),
				Name = sourceLocation.Name,
				Type = sourceLocation.Type, // LocationTypes are global, not season-scoped
				Season = targetSeason,
				Containers = new List<ShiftContainerEntity>()
			};
			Database.ShiftLocations.Add(newLocation);
			locationMapping[sourceLocation.Id] = newLocation;
		}

		// 4. Copy Containers with date offset
		var sourceContainers = await Database.Containers
			.Include(c => c.Framework)
			.Include(c => c.Location)
			.Where(c => c.Framework.Season.Id == sourceSeasonId)
			.ToListAsync(ct);

		foreach (var sourceContainer in sourceContainers)
		{
			if (frameworkMapping.TryGetValue(sourceContainer.Framework.Id, out var newFramework) &&
			    locationMapping.TryGetValue(sourceContainer.Location.Id, out var newLocation))
			{
				var newContainer = new ShiftContainerEntity
				{
					Id = Guid.NewGuid(),
					Start = sourceContainer.Start + dateOffset,
					End = sourceContainer.End + dateOffset,
					TotalShifts = sourceContainer.TotalShifts,
					Color = sourceContainer.Color,
					Framework = newFramework,
					Location = newLocation,
					Shifts = new List<ShiftEntity>() // Don't copy individual shift assignments
				};
				Database.Containers.Add(newContainer);
				newLocation.Containers.Add(newContainer);
			}
		}

		// 5. Copy promotion channels and schedule. They start disabled, so nothing is posted to last
		// event's groups by accident; secrets stay encrypted with the same key ring.
		var sourceChannels = await Database.PromotionChannels
			.Where(c => c.Season.Id == sourceSeasonId)
			.AsNoTracking()
			.ToListAsync(ct);
		foreach (var sourceChannel in sourceChannels)
		{
			Database.PromotionChannels.Add(new PromotionChannelEntity
			{
				Season = targetSeason,
				Name = sourceChannel.Name,
				Kind = sourceChannel.Kind,
				Enabled = false,
				Config = sourceChannel.Config with
				{
					LocationIds = sourceChannel.Config.LocationIds
						.Where(locationMapping.ContainsKey)
						.Select(id => locationMapping[id].Id)
						.ToList()
				},
				EncryptedSecret = sourceChannel.EncryptedSecret,
				SecretHint = sourceChannel.SecretHint
			});
		}

		var sourceSchedule = await Database.PromotionSchedules
			.AsNoTracking()
			.FirstOrDefaultAsync(s => s.Season.Id == sourceSeasonId, ct);
		if (sourceSchedule is not null)
		{
			Database.PromotionSchedules.Add(new PromotionScheduleEntity
			{
				Season = targetSeason,
				Settings = sourceSchedule.Settings with { Enabled = false }
			});
		}
	}
}