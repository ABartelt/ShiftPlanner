using ClosedXML.Excel;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Muddi.ShiftPlanner.Server.Database.Entities;

namespace Muddi.ShiftPlanner.Server.Api.Services;

public class ExcelService
{
	private readonly IKeycloakService _keycloakService;

	public ExcelService(IKeycloakService keycloakService)
	{
		_keycloakService = keycloakService;
	}


	public async Task<byte[]> ExportLocationToXlsxAsync(ShiftLocationEntity location, bool anonymous = false)
	{
		const int startRow = 5;
		using var book = new XLWorkbook("Templates/template.xlsx");
		var containerGroups = location.Containers.GroupBy(c => c.Start.Date).OrderBy(g => g.Key);

		var availableShifts = location.Containers
			.SelectMany(container => container.GetStartTimes()
				.SelectMany(startTime => container.GetAvailableShiftTypes(startTime)))
			.ToArray();

		foreach (var containerGroup in containerGroups)
		{
			var date = containerGroup.Key.Date;
			var dateStr = containerGroup.Key.ToString("ddd dd-MM");
			var worksheet = book.Worksheets.First().CopyTo(dateStr);

			worksheet.Cell("A1").StringReplace("{{DATE}}", dateStr);
			worksheet.Cell("A2").StringReplace("{{LOCATION}}", location.Name);


			var shifts = containerGroup.SelectMany(c => c.Shifts).ToArray();


			int startCell = 2;
			int row = startRow;
			Dictionary<Guid, int> typesToCell = new();
			var shiftGroups = availableShifts
				.Where(s => s.Start.Date == date)
				.GroupBy(s => s.Start)
				.OrderBy(g => g.Key);
			foreach (var typesGroup in shiftGroups)
			{
				foreach (var types in typesGroup)
				{
					if (!typesToCell.TryGetValue(types.Type.Id, out var cell))
					{
						cell = startCell++;
						typesToCell[types.Type.Id] = cell;
						worksheet.Cell(4, cell).SetValue(types.Type.Name);
					}


#warning Timezone warning, AddHours(2) is plain stupid!
					//TODO Set the timezone of the docker container, and then remove AddHours(2) and
					//replace it with .ToLocalTime()
					worksheet.Cell(row, 1).Value = typesGroup.Key.AddHours(2).ToString("HH:mm");

					if (!anonymous)
					{
						foreach (var shift in shifts.Where(s => s.Type.Id == types.Type.Id
						                                        && s.Start == typesGroup.Key))
						{
							var user = await _keycloakService.GetUserByIdAsync(shift.EmployeeKeycloakId);
							worksheet.Cell(row, cell).GetRichText().AddText($"{user.FirstName} {user.LastName?[0]}.")
								.AddNewLine();
						}
					}

					var cnt = typesGroup.Where(g => g.Type.Id == types.Type.Id).Sum(g => g.AvailableCount);
					if (cnt > 0)
					{
						worksheet.Cell(row, cell).GetRichText().AddText($"({cnt} FREI)").SetFontSize(8);
					}
					else
					{
						if (anonymous)
						{
							worksheet.Cell(row, cell).GetRichText().AddText("VOLL BESETZT");
						}
						else
						{
							worksheet.Cell(row, cell).GetRichText().AddText("(VOLL BESETZT)").SetFontSize(8);
						}
					}
				}

				row++;
			}
		}

		//Delete template
		if (book.Worksheets.Count > 1)
			book.Worksheets.First().Delete();

		using var ms = new MemoryStream();
		book.SaveAs(ms);
		return ms.ToArray();
	}
}

public static class ClosedXMLExtensions
{
	public static void StringReplace(this IXLCell cell, string oldValue, string newValue)
	{
		cell.Value = cell.GetString().Replace(oldValue, newValue);
	}
}