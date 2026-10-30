namespace Muddi.ShiftPlanner.Shared.Contracts.v1.Requests;

public class UpdateContainerRequest
{
	public Guid Id { get; set; }
	public Guid FrameworkId { get; set; }
	public DateTime Start { get; set; }
	public int TotalShifts { get; set; }
	public string Color { get; set; }
}
