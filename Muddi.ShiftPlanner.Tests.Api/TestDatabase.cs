using Npgsql;
using Testcontainers.PostgreSql;

namespace Muddi.ShiftPlanner.Tests.Api;

/// <summary>
/// A throw-away Postgres database for one test run. By default a <c>postgres:14</c> container is started via
/// Testcontainers (needs Docker). Set <see cref="AdminConnectionStringVariable"/> to an admin connection string of
/// an existing server (e.g. <c>Host=127.0.0.1;Username=postgres;Password=;Database=postgres</c>) to create a
/// temporary database there instead, which is handy on machines or CI runners without Docker.
/// </summary>
public sealed class TestDatabase : IAsyncDisposable
{
	public const string AdminConnectionStringVariable = "SHIFTPLANNER_TEST_DB_ADMIN";

	// Same image as Docker/development/muddi-shift-planner-dev/docker-compose.yml
	private readonly PostgreSqlContainer? _container;
	private readonly string? _adminConnectionString;
	private readonly string _databaseName = "shiftplanner_test_" + Guid.NewGuid().ToString("N")[..12];

	public TestDatabase()
	{
		_adminConnectionString = Environment.GetEnvironmentVariable(AdminConnectionStringVariable);
		if (string.IsNullOrWhiteSpace(_adminConnectionString))
			_container = new PostgreSqlBuilder().WithImage("postgres:14").Build();
	}

	public string ConnectionString { get; private set; } = string.Empty;

	public async Task StartAsync()
	{
		if (_container is not null)
		{
			await _container.StartAsync();
			ConnectionString = _container.GetConnectionString();
			return;
		}

		await using (var admin = new NpgsqlConnection(_adminConnectionString))
		{
			await admin.OpenAsync();
			await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", admin);
			await create.ExecuteNonQueryAsync();
		}

		ConnectionString = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = _databaseName }
			.ConnectionString;
	}

	public async ValueTask DisposeAsync()
	{
		if (_container is not null)
		{
			await _container.DisposeAsync();
			return;
		}

		NpgsqlConnection.ClearAllPools();
		await using var admin = new NpgsqlConnection(_adminConnectionString);
		await admin.OpenAsync();
		await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", admin);
		await drop.ExecuteNonQueryAsync();
	}
}
