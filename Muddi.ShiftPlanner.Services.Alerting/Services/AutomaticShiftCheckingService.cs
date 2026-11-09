using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Server.Database.Entities;
using Muddi.ShiftPlanner.Services.Alerting.Notifications;
using Muddi.ShiftPlanner.Services.Alerting.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Muddi.ShiftPlanner.Services.Alerting.Services;

/// <summary>
/// Posts the free shifts of the next day to a Telegram group once a day.
/// </summary>
public class AutomaticShiftCheckingService : BackgroundService
{
	private readonly ITelegramBotClient _telegramClient;
	private readonly IServiceScopeFactory _scopes;
	private readonly ShiftFormatter _formatter;
	private readonly TelegramOptions _options;
	private readonly ILogger<AutomaticShiftCheckingService> _logger;
	private readonly DateTime? _startDate;

	public AutomaticShiftCheckingService(ITelegramBotClient telegramClient, IServiceScopeFactory scopes,
		ShiftFormatter formatter, IOptions<TelegramOptions> options, ILogger<AutomaticShiftCheckingService> logger)
	{
		_telegramClient = telegramClient;
		_scopes = scopes;
		_formatter = formatter;
		_options = options.Value;
		_logger = logger;
		_startDate = string.IsNullOrEmpty(_options.StartDate)
			? null
			: DateTime.ParseExact(_options.StartDate, "dd.MM.yyyy", CultureInfo.InvariantCulture);
	}

	public static TimeSpan CalculateDueTime(TimeSpan timeOfDayToStart)
	{
		var due = timeOfDayToStart > DateTime.UtcNow.TimeOfDay
			? timeOfDayToStart - DateTime.UtcNow.TimeOfDay
			: timeOfDayToStart.Add(TimeSpan.FromDays(1)) - DateTime.UtcNow.TimeOfDay;

		return due;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		try
		{
			if (_options.SendOnProgramStart)
				await Execute(stoppingToken);
			while (!stoppingToken.IsCancellationRequested)
			{
				await Task.Delay(CalculateDueTime(_options.SendTime), stoppingToken);
				await Execute(stoppingToken);
			}
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
		}
	}

	private async Task Execute(CancellationToken ct)
	{
		try
		{
			var tomorrow = _formatter.ToLocal(DateTime.UtcNow).Date.AddDays(1);
			if (_startDate is not null && tomorrow < _startDate)
				tomorrow = _startDate.Value;

			var freeByLocation = await CountFreeShiftsAsync(tomorrow, ct);
			if (freeByLocation.Count == 0)
			{
				_logger.LogInformation("No free shifts for {Tomorrow}", tomorrow);
				await _telegramClient.SendMessage(_options.GroupId,
					$"Alle Schichten für {ReminderTexts.Day(tomorrow)} besetzt :)", cancellationToken: ct);
			}
			else
			{
				_logger.LogInformation("Found {N} free shifts for tomorrow", freeByLocation.Sum(l => l.Free));
				await _telegramClient.SendMessage(_options.GroupId, CreateTextForBot(freeByLocation, tomorrow),
					ParseMode.Html, cancellationToken: ct);
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Failed to send the free shifts summary");
		}
	}

	private async Task<List<(ShiftLocationEntity Location, int Free)>> CountFreeShiftsAsync(DateTime localDay,
		CancellationToken ct)
	{
		await using var scope = _scopes.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<ShiftPlannerContext>();
		var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(localDay, _formatter.Zone);
		var dayEndUtc = TimeZoneInfo.ConvertTimeToUtc(localDay.AddDays(1), _formatter.Zone);
		var containers = await db.Containers.AsNoTracking()
			.Include(c => c.Location)
			.Include(c => c.Framework).ThenInclude(f => f.ShiftTypeCounts).ThenInclude(stc => stc.ShiftType)
			.Include(c => c.Shifts).ThenInclude(s => s.Type)
			.Where(c => c.Location.Season.IsSelectedAsCurrent && c.Start < dayEndUtc && c.End > dayStartUtc)
			.AsSplitQuery()
			.ToListAsync(ct);

		return containers
			.GroupBy(c => c.Location.Id)
			.Select(g => (g.First().Location, Free: g.Sum(c => FreeShifts(c, dayStartUtc, dayEndUtc))))
			.Where(l => l.Free > 0)
			.OrderBy(l => l.Location.Name)
			.ToList();
	}

	/// <summary>
	/// Free places in the slots starting that day; admin-only types and blocked places don't count as free.
	/// </summary>
	public static int FreeShifts(ShiftContainerEntity container, DateTime dayStartUtc, DateTime dayEndUtc)
	{
		var free = 0;
		for (var i = 0; i < container.TotalShifts; i++)
		{
			var slotStart = container.Start + container.Framework.TimePerShift * i;
			if (slotStart < dayStartUtc || slotStart >= dayEndUtc)
				continue;
			foreach (var typeCount in container.Framework.ShiftTypeCounts.Where(t => !t.ShiftType.OnlyAssignableByAdmin))
			{
				var taken = container.Shifts.Count(s => s.Start == slotStart && s.Type.Id == typeCount.ShiftType.Id);
				free += Math.Max(0, typeCount.Count - taken);
			}
		}

		return free;
	}

	private string CreateTextForBot(IReadOnlyCollection<(ShiftLocationEntity Location, int Free)> locations,
		DateTime tomorrow)
	{
		StringBuilder sb = new();
		sb.Append(
			$"⚠️Für den nächsten Tag ({ReminderTexts.Day(tomorrow)}) gibt es noch {locations.Sum(l => l.Free)} freie Schichten.⚠️\n");
		foreach (var (location, free) in locations)
		{
			var url = _formatter.LocationDayLink(location.Id, tomorrow);
			sb.Append($"\t<a href=\"{WebUtility.HtmlEncode(url)}\">{WebUtility.HtmlEncode(location.Name)}</a>:\t{free} freie Schichten\n");
		}

		return sb.ToString();
	}
}
