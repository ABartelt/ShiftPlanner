using System.ComponentModel.DataAnnotations;
using Muddi.ShiftPlanner.Shared.Entities;

namespace Muddi.ShiftPlanner.Server.Database.Entities;

/// <summary>
/// One message sent (or attempted) to a promotion channel.
/// </summary>
public class PromotionLogEntity
{
	public Guid Id { get; set; }
	public required SeasonEntity Season { get; set; }
	public PromotionChannelEntity? Channel { get; set; }
	[MaxLength(100)]
	public required string ChannelName { get; set; }
	public PromotionRunKind RunKind { get; set; }

	/// <summary>
	/// Identifies a scheduled run (e.g. "daily:2026-06-17") so it is posted only once per channel.
	/// </summary>
	[MaxLength(100)]
	public required string RunKey { get; set; }

	public DateTime SentAt { get; set; }
	public int OpenShifts { get; set; }
	public PromotionLogStatus Status { get; set; }
	[MaxLength(500)]
	public string? Error { get; set; }
}
