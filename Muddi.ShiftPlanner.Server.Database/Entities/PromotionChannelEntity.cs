using System.ComponentModel.DataAnnotations;
using Muddi.ShiftPlanner.Shared.Entities;

namespace Muddi.ShiftPlanner.Server.Database.Entities;

/// <summary>
/// A messenger group/channel that open shifts of one season (event) are promoted to.
/// </summary>
public class PromotionChannelEntity
{
	public Guid Id { get; set; }
	public required SeasonEntity Season { get; set; }
	[MaxLength(100)]
	public required string Name { get; set; }
	public PromotionChannelKind Kind { get; set; }
	public bool Enabled { get; set; }

	/// <summary>
	/// Non-secret settings (target chat/group, Signal sender, location filter), stored as <c>jsonb</c>.
	/// </summary>
	public PromotionChannelConfig Config { get; set; } = new();

	/// <summary>
	/// Bot token or webhook URL, encrypted with ASP.NET Core Data Protection. Never sent to clients.
	/// </summary>
	public string? EncryptedSecret { get; set; }

	/// <summary>
	/// Masked version of the secret (e.g. "…a91f") so admins can recognize it.
	/// </summary>
	[MaxLength(50)]
	public string? SecretHint { get; set; }

	public DateTime? LastTestAt { get; set; }
	public bool? LastTestOk { get; set; }
	[MaxLength(500)]
	public string? LastTestMessage { get; set; }
}
