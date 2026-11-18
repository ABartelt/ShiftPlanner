using System.Text.Json;
using FluentAssertions;
using Muddi.ShiftPlanner.Shared.Entities;
using Xunit;

namespace Muddi.ShiftPlanner.Tests.Unit;

public class JsonTests
{
	[Fact]
	public void ShouldDeserializeApplicationSettings_WhenValid()
	{
		string json = """
		              {
		              "Title": "MUDDIs Schicht Planner",
		              "Subtitle": "Auf eine HAMMER-mäßige Kieler Woche 2023!",
		              "Contact": "gastro@muddimarkt.org",
		              "Greeting": "Buongiorno",
		              "MemberName": "MUDDIs",
		              "StartTime": "08:00:00",
		              "EndTime": "1.02:00:00"
		              }
		              """;

		var settings = JsonSerializer.Deserialize<ApplicationSettings>(json);

		settings.Should().NotBeNull();
		settings!.Title.Should().Be("MUDDIs Schicht Planner");
		settings.Subtitle.Should().Be("Auf eine HAMMER-mäßige Kieler Woche 2023!");
		settings.Contact.Should().Be("gastro@muddimarkt.org");
		settings.Greeting.Should().Be("Buongiorno");
		settings.MemberName.Should().Be("MUDDIs");
		settings.StartTime.TotalHours.Should().Be(8);
		settings.EndTime.TotalHours.Should().Be(26);
	}

	[Fact]
	public void ShouldDeserializeApplicationSettings_WithMinValues()
	{
		string json = """
		              {
		              "Title": "Test Planner",
		              "Subtitle": "",
		              "Contact": "test@example.com",
		              "Greeting": "",
		              "MemberName": "TestGroup",
		              "StartTime": "00:00:00",
		              "EndTime": "00:00:00"
		              }
		              """;

		var settings = JsonSerializer.Deserialize<ApplicationSettings>(json);

		settings.Should().NotBeNull();
		settings!.Title.Should().Be("Test Planner");
		settings.Subtitle.Should().Be("");
		settings.Contact.Should().Be("test@example.com");
		settings.Greeting.Should().Be("");
		settings.MemberName.Should().Be("TestGroup");
		settings.StartTime.TotalHours.Should().Be(0);
		settings.EndTime.TotalHours.Should().Be(0);
	}

	[Fact]
	public void ShouldSerializeEndTimePastMidnight_AsLenientHours()
	{
		// LenientTimeSpanConverter writes hours past 23 as "26:00:00"; the Vue client (frontend/src/domain/time.ts) reads that.
		var json = JsonSerializer.Serialize(new ApplicationSettings());

		json.Should().Contain("\"EndTime\":\"26:00:00\"");
		JsonSerializer.Deserialize<ApplicationSettings>(json)!.EndTime.TotalHours.Should().Be(26);
	}

	[Theory]
	[InlineData("26:00:00")]
	[InlineData("1.02:00:00")]
	public void ShouldDeserializeEndTimePastMidnight_InBothFormats(string endTime)
	{
		// The Vue client still writes "d.hh:mm:ss" (minutesToDuration), the Blazor client writes "26:00:00"; both must parse.
		var json = "{\"EndTime\":\"" + endTime + "\"}";

		JsonSerializer.Deserialize<ApplicationSettings>(json)!.EndTime.TotalHours.Should().Be(26);
	}
}
