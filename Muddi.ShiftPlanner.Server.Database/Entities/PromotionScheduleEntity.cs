using Muddi.ShiftPlanner.Shared.Entities;

namespace Muddi.ShiftPlanner.Server.Database.Entities;

/// <summary>
/// When open shifts of one season (event) are posted automatically. One row per season.
/// </summary>
public class PromotionScheduleEntity
{
	public Guid Id { get; set; }
	public required SeasonEntity Season { get; set; }

	/// <summary>
	/// Schedule settings, stored as <c>jsonb</c>.
	/// </summary>
	public PromotionSchedule Settings { get; set; } = new();
}
