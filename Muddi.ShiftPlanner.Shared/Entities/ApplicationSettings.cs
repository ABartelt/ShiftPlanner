namespace Muddi.ShiftPlanner.Shared.Entities;

/// <summary>
/// App-wide settings stored as JSONB in the database.
/// Used for settings that should be editable via the UI.
/// </summary>
public record ApplicationSettings
{
	/// <summary>
	/// Application title shown in the header.
	/// </summary>
	public string Title { get; set; } = "MUDDIs Schicht Planner";

	/// <summary>
	/// Subtitle shown on the help page.
	/// </summary>
	public string Subtitle { get; set; } = string.Empty;

	/// <summary>
	/// Contact email for support.
	/// </summary>
	public string Contact { get; set; } = string.Empty;

	/// <summary>
	/// Daily start time for shift containers. Defaults to 08:00.
	/// </summary>
	public TimeSpan StartTime { get; set; } = new(8, 0, 0);

	/// <summary>
	/// Daily end time for shift containers. Defaults to 26:00 (02:00 the next day).
	/// </summary>
	public TimeSpan EndTime { get; set; } = new(26, 0, 0);

	/// <summary>
	/// Greeting word (e.g. "Moin").
	/// </summary>
	public string Greeting { get; set; } = "Moin";

	/// <summary>
	/// Member/organization name (e.g. "MUDDIs").
	/// </summary>
	public string MemberName { get; set; } = "MUDDIs";

	/// <summary>
	/// When enabled, visitors without a login may browse the current season's schedule through the
	/// anonymous <c>/public/*</c> API: locations, free/occupied slot counts per time and shift type,
	/// and the next free shifts. Never exposes who booked a shift, block reasons, admin-only locations
	/// or admin-only shift types. Default is off so a fresh deployment stays private.
	/// </summary>
	public bool PublicBrowsingEnabled { get; set; } = false;

	/// <summary>
	/// Generates a mailto: link for the contact email, or "#" if empty.
	/// </summary>
	public string ContactHref => string.IsNullOrEmpty(Contact) || Contact.Contains('@') ? (string.IsNullOrEmpty(Contact) ? "#" : "mailto:" + Contact) : Contact;
}
