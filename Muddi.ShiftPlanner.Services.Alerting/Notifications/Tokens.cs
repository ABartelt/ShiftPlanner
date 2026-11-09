using System.Security.Cryptography;

namespace Muddi.ShiftPlanner.Services.Alerting.Notifications;

public static class Tokens
{
	/// <summary>
	/// 256 bit, URL safe.
	/// </summary>
	public static string Create() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
		.TrimEnd('=').Replace('+', '-').Replace('/', '_');

	public static string CreateActionCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}
