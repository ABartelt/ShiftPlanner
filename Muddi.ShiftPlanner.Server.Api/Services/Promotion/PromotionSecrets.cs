using Microsoft.AspNetCore.DataProtection;

namespace Muddi.ShiftPlanner.Server.Api.Services.Promotion;

/// <summary>
/// Encrypts channel secrets (bot tokens, webhook URLs) at rest. The key ring lives in the database.
/// </summary>
public class PromotionSecrets(IDataProtectionProvider provider)
{
	private readonly IDataProtector _protector = provider.CreateProtector("Muddi.ShiftPlanner.Promotion.Secrets");

	public string Protect(string secret) => _protector.Protect(secret);

	public string? Unprotect(string? encrypted)
	{
		if (string.IsNullOrEmpty(encrypted))
			return null;
		try
		{
			return _protector.Unprotect(encrypted);
		}
		catch (System.Security.Cryptography.CryptographicException)
		{
			// Key ring lost or replaced: the admin has to enter the secret again.
			return null;
		}
	}

	/// <summary>
	/// Last characters of a secret, so admins can tell which one is stored.
	/// </summary>
	public static string Hint(string secret) => secret.Length <= 8 ? "…" : "…" + secret[^4..];
}
