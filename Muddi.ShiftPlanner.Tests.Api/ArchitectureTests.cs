using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Muddi.ShiftPlanner.Server.Api.Endpoints.Public;
using Muddi.ShiftPlanner.Shared.Contracts.v1.Responses;
using Xunit;

namespace Muddi.ShiftPlanner.Tests.Api;

/// <summary>
/// Guards the privacy boundary structurally, independent of test data: only <c>/public/*</c> and <c>/version</c>
/// may be anonymous, and no public response type can carry personal data.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ArchitectureTests
{
	private readonly ApiFactory _factory;

	public ArchitectureTests(ApiFactory factory)
	{
		_factory = factory;
	}

	[Fact]
	public void OnlyPublicRoutesAndVersion_AllowAnonymous()
	{
		var anonymousRoutes = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
			.OfType<RouteEndpoint>()
			.Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
			.Select(e => "/" + (e.RoutePattern.RawText ?? string.Empty).TrimStart('/'))
			.Distinct()
			.ToList();

		anonymousRoutes.Should().NotBeEmpty();
		anonymousRoutes.Should().OnlyContain(r => r.StartsWith("/public/") || r == "/version",
			"every anonymous endpoint must live under /public/ so the privacy review has one place to look");
	}

	private static readonly Regex ForbiddenPropertyNames =
		new("^(Employee.*|BlockReason|Email|FirstName|LastName|FullName)$", RegexOptions.Compiled);

	private static readonly Type[] ForbiddenTypes = [typeof(GetShiftResponse), typeof(GetEmployeeResponse)];

	[Fact]
	public void PublicEndpointResponseTypes_HaveNoPersonalProperties()
	{
		var apiAssembly = typeof(Program).Assembly;
		var responseTypes = apiAssembly.GetTypes()
			.Select(ResponseTypeOfPublicEndpoint)
			.Where(t => t is not null)
			.Cast<Type>()
			.Append(typeof(GetPublicInfoResponse))
			.Distinct()
			.ToList();

		responseTypes.Should().HaveCountGreaterThan(3, "the public endpoints should be discovered");

		foreach (var responseType in responseTypes)
		{
			var visited = new HashSet<Type>();
			var violations = new List<string>();
			Walk(responseType, responseType.Name, visited, violations);
			violations.Should().BeEmpty($"public response {responseType.Name} must not expose personal data");
		}
	}

	private static Type? ResponseTypeOfPublicEndpoint(Type type)
	{
		for (var t = type.BaseType; t is not null; t = t.BaseType)
		{
			if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(PublicEndpoint<,>))
				return t.GetGenericArguments()[1];
		}

		return null;
	}

	private static void Walk(Type type, string path, HashSet<Type> visited, List<string> violations)
	{
		type = Nullable.GetUnderlyingType(type) ?? type;
		if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(Guid) || type == typeof(DateTime) ||
		    type == typeof(TimeSpan) || type == typeof(decimal) || type == typeof(object))
			return;
		if (type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type))
		{
			var element = type.IsArray
				? type.GetElementType()!
				: type.GetInterfaces().Concat([type])
					.FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
					?.GetGenericArguments()[0];
			if (element is not null)
				Walk(element, path + "[]", visited, violations);
			return;
		}

		if (!visited.Add(type))
			return;
		if (ForbiddenTypes.Contains(type))
			violations.Add($"{path} is {type.Name}");

		foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			var propertyPath = $"{path}.{property.Name}";
			if (ForbiddenPropertyNames.IsMatch(property.Name))
				violations.Add(propertyPath);
			Walk(property.PropertyType, propertyPath, visited, violations);
		}
	}
}
