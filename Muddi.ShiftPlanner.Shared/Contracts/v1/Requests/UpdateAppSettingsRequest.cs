namespace Muddi.ShiftPlanner.Shared.Contracts.v1.Requests;

public class UpdateAppSettingsRequest
{
	/// <summary>
	/// Application title shown in the header.
	/// </summary>
	public string Title { get; init; } = "MUDDIs Schicht Planner";

	/// <summary>
	/// Subtitle shown on the help page.
	/// </summary>
	public string Subtitle { get; init; } = string.Empty;

	/// <summary>
	/// Contact email for support.
	/// </summary>
	public string Contact { get; init; } = string.Empty;

	/// <summary>
	/// Daily start time for shift containers (e.g. "08:00:00")
	/// </summary>
	public TimeSpan StartTime { get; init; } = new(8, 0, 0);

	/// <summary>
	/// Daily end time for shift containers (e.g. "26:00:00" for 2:00 AM next day)
	/// </summary>
	public TimeSpan EndTime { get; init; } = new(26, 0, 0);

	/// <summary>
	/// Greeting word (e.g. "Moin").
	/// </summary>
	public string Greeting { get; init; } = "Moin";

	/// <summary>
	/// Member/organization name (e.g. "MUDDIs").
	/// </summary>
	public string MemberName { get; init; } = "MUDDIs";

	/// <summary>
	/// Allow anonymous browsing of free shifts via the public API. See <see cref="Entities.ApplicationSettings.PublicBrowsingEnabled"/>.
	/// </summary>
	public bool PublicBrowsingEnabled { get; init; } = false;
}