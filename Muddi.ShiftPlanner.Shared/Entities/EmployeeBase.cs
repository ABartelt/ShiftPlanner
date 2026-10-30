namespace Muddi.ShiftPlanner.Shared.Entities;

public record ShiftType(
	Guid Id,
	string Name,
	string Color,
	bool OnlyAssignableByAdmin,
	TimeSpan StartingTimeShift,
	string? Description);

public enum UserRoles
{
	Viewer,
	Worker,
	Manager,
	Admin
}

public class NotAssignedEmployee : Employee
{
	public NotAssignedEmployee() : base(Guid.Empty, string.Empty, "FREI", null)
	{
	}
}

/// <summary>
/// Placeholder for a place that is taken by someone whose identity the viewer may not see
/// (anonymous browsing). Carries no name and no real id.
/// </summary>
public class OccupiedEmployee : Employee
{
	public static readonly Guid SentinelId = new("00000000-0000-0000-0000-000000000001");

	public OccupiedEmployee() : base(SentinelId, string.Empty, "Besetzt", null)
	{
	}
}

public class Employee : EmployeeBase
{
	public string Email { get; }
	public string FirstName { get; }
	public string LastName { get; }


	public Employee(Guid keycloakId, string email, string fullName)
		: this(keycloakId, email, fullName[..(fullName.IndexOf(' '))], fullName[(fullName.IndexOf(' ') + 1)..])
	{
		
	}

	public Employee(Guid keycloakId, string email, string? firstName, string? lastName)
		: base(keycloakId, $"{firstName} {lastName?.FirstOrDefault()}")
	{
		Email = email;
		FirstName = firstName ?? string.Empty;
		LastName = lastName ?? string.Empty;
	}
}

public abstract class EmployeeBase : IEquatable<EmployeeBase>
{
	public EmployeeBase(Guid keycloakId, string name)
	{
		KeycloakId = keycloakId;
		Name = name;
	}

	public Guid KeycloakId { get; }
	public string Name { get; }


	public bool Equals(EmployeeBase? other)
	{
		if (ReferenceEquals(null, other)) return false;
		if (ReferenceEquals(this, other)) return true;
		return KeycloakId.Equals(other.KeycloakId);
	}

	public override bool Equals(object? obj)
	{
		if (ReferenceEquals(null, obj)) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != GetType()) return false;
		return Equals((EmployeeBase)obj);
	}

	public override int GetHashCode()
	{
		return KeycloakId.GetHashCode();
	}

	public static bool operator ==(EmployeeBase? left, EmployeeBase? right)
	{
		return Equals(left, right);
	}

	public static bool operator !=(EmployeeBase? left, EmployeeBase? right)
	{
		return !Equals(left, right);
	}

	public override string ToString() => $"{Name} ({KeycloakId})";
}
