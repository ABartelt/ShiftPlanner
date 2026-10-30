using Muddi.ShiftPlanner.Client.Entities;
using Muddi.ShiftPlanner.Shared;
using Muddi.ShiftPlanner.Shared.Entities;
using Radzen;
using Radzen.Blazor.Rendering;
using Appointment = Muddi.ShiftPlanner.Client.Entities.Appointment;

namespace Muddi.ShiftPlanner.Client;

public static class RenderExtensions
{
	public static void SetSlotRenderStyle(this SchedulerSlotRenderEventArgs args,
		IReadOnlyList<ShiftContainer> containers)
	{
		switch (args.View?.Text)
		{
			// Highlight today in month view
			case "Monat" when args.Start.Date == DateTime.Today:
				args.Attributes["style"] = "background: rgba(255,220,40,.2);";
				break;
			// Highlight shift hours
			case "Woche" or "Tag":
			{
				foreach (var container in containers)
				{
					var c = container.BackgroundColor;
					var maxTimeShift = container.Framework.RolesCount.Select(x => x.Key.StartingTimeShift)
						.DefaultIfEmpty(TimeSpan.Zero).Max();

					if (args.Start >= container.StartTime.ToLocalTime() &&
					    args.Start < container.EndTime.Add(maxTimeShift).ToLocalTime())
						args.Attributes["style"] = $"background: {c};";
				}

				break;
			}
			default: 
				throw new NotSupportedException($"View {args.View?.Text} not supported");
		}
	}

	/// <param name="userKeycloakId">The viewer's id, or null for anonymous visitors (no own-shift highlighting).</param>
	public static void SetAppointmentRenderStyle(this SchedulerAppointmentRenderEventArgs<Appointment> args,
		Guid? userKeycloakId)
	{
		args.Attributes["style"] = string.Empty;

		if (args.Data is WeekAppointment weekAppointment)
		{
			var from = weekAppointment.AvailableShifts;
			var to = weekAppointment.TotalShifts;

			double num = from == 0 || to == 0
				? .4
				: Convert.ToInt32(40 + 40 * from / to) / 100.0;
			args.Attributes["style"] = $"background: rgb(0,0,0,{num.ToInvariantString()});";
			
			return;
		}

		if (args.Data is not DayAppointment dayAppointment)
			throw new NotSupportedException("Unknown appointment type" + args.Data?.GetType());
		var shift = dayAppointment.Shift;

		//DayView
		// Blocked shifts - check before free shifts, since blocked shifts also have NotAssignedEmployee as user
		if (shift.IsBlocked)
		{
			var color = !string.IsNullOrEmpty(shift.Type.Color)
				? shift.Type.Color
				: "#666";
			args.Attributes["style"] += $"""
			                             color: #666;
			                             border: 1px solid {color};
			                             background: repeating-linear-gradient(

			                               45deg,
			                               #ebebeb,       /* Stripe 1 color */
			                               #ebebeb 15px,  /* Stripe 1 width */
			                               #cdcdcd 15px,  /* Stripe 2 color starts immediately */
			                               #cdcdcd 30px   /* Stripe 2 width ends */
			                             );
			                             """;
			args.Attributes["title"] = "Gesperrt: " + shift.BlockReason;
			return;
		}

		// Free shifts - plain white
		if (shift.User == Mappers.NotAssignedEmployee)
		{
			var textColor = !string.IsNullOrEmpty(shift.Type.Color)
				? shift.Type.Color
				: "#333333";
			args.Attributes["style"] += $"background: #ffffff; color:{textColor}; border: 1px solid {textColor};";
			return;
		}

		// Occupied by somebody the viewer may not see (anonymous browsing) - muted, not clickable
		if (shift.User == Mappers.OccupiedEmployee)
		{
			var borderColor = !string.IsNullOrEmpty(shift.Type.Color) ? shift.Type.Color : "#999999";
			args.Attributes["style"] +=
				$"background: #e9e9e9; color: #777777; border: 1px solid {borderColor}; cursor: default;";
			args.Attributes["title"] = "Diese Schicht ist schon besetzt";
			return;
		}

		var normalBg = !string.IsNullOrEmpty(shift.Type.Color) ? shift.Type.Color : "#cccccc";
		args.Attributes["style"] += $"background: {normalBg};";
		if (userKeycloakId is { } me && shift.User.KeycloakId == me)
		{
			args.Attributes["style"] += "outline: 2px dashed #852121;";
		}
	}
}