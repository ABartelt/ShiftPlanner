using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Muddi.ShiftPlanner.Shared.Entities;

/// <summary>
/// Reads and writes TimeSpans as "HH:mm:ss" where hours may exceed 23 (e.g. "26:00:00").
/// </summary>
public class LenientTimeSpanConverter : JsonConverter<TimeSpan>
{
	public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var text = reader.GetString();
		if (string.IsNullOrWhiteSpace(text))
			throw new JsonException("TimeSpan value is empty.");

		var negative = text.StartsWith('-');
		var parts = text.TrimStart('-').Split(':');
		if (parts.Length is 2 or 3
		    && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
		    && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
		    && minutes < 60)
		{
			var seconds = 0;
			if (parts.Length == 3 && (!int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture,
				    out seconds) || seconds >= 60))
				throw new JsonException($"'{text}' is not a valid TimeSpan.");

			var result = new TimeSpan(hours, minutes, seconds);
			return negative ? result.Negate() : result;
		}

		if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var parsed))
			return parsed;
		throw new JsonException($"'{text}' is not a valid TimeSpan.");
	}

	public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
	{
		var abs = value.Duration();
		var hours = (long)abs.TotalHours;
		writer.WriteStringValue(
			$"{(value < TimeSpan.Zero ? "-" : "")}{hours:00}:{abs.Minutes:00}:{abs.Seconds:00}");
	}
}
