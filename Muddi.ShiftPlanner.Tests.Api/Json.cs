using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Muddi.ShiftPlanner.Tests.Api;

internal static class Json
{
	public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

	public static Task<T?> GetAsync<T>(this HttpClient client, string url)
		=> client.GetFromJsonAsync<T>(url, Options);

	public static async Task<JsonNode?> GetNodeAsync(this HttpClient client, string url)
	{
		var body = await client.GetStringAsync(url);
		return JsonNode.Parse(body);
	}
}
