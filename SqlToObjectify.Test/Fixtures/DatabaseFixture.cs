using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SqlToObjectify.Test.Fixtures;

/// <summary>
/// Shared test fixture: creates the LocalDB database, applies EF migrations/seed,
/// and creates stored procedures once for all tests in the collection.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    public const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=SqlToObjectifyTestDb;Integrated Security=True;Encrypt=False;";

    private SqlObjectDbContext? _context;

    public SqlObjectDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SqlObjectDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new SqlObjectDbContext(options);
    }

    public async Task InitializeAsync()
    {
        _context = CreateContext();

        // Ensure clean DB: drop and recreate to guarantee schema + seed data.
        await _context.Database.EnsureDeletedAsync();
        await _context.Database.EnsureCreatedAsync();

        // Create stored procedures for SP tests.
        await CreateStoredProceduresAsync();
    }

    public async Task DisposeAsync()
    {
        if (_context is not null)
        {
            await _context.Database.EnsureDeletedAsync();
            await _context.DisposeAsync();
        }
    }

    private async Task CreateStoredProceduresAsync()
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        // SP: GetEmployeesByDepartmentId — returns employees filtered by department
        await ExecuteSqlAsync(conn, """
            CREATE PROCEDURE [dbo].[GetEmployeesByDepartmentId]
                @DepartmentId INT
            AS
            BEGIN
                SET NOCOUNT ON;
                SELECT Id, Name, DepartmentId
                FROM Employees
                WHERE DepartmentId = @DepartmentId;
            END
            """);

        // SP: GetDepartmentEmployeeCounts — returns department names with employee counts
        await ExecuteSqlAsync(conn, """
            CREATE PROCEDURE [dbo].[GetDepartmentEmployeeCounts]
            AS
            BEGIN
                SET NOCOUNT ON;
                SELECT d.Name AS DepartmentName, COUNT(e.Id) AS NumberOfEmployee
                FROM Departments d
                LEFT JOIN Employees e ON e.DepartmentId = d.Id
                GROUP BY d.Name;
            END
            """);

        // SP: GetEmployeeById — returns a single employee row
        await ExecuteSqlAsync(conn, """
            CREATE PROCEDURE [dbo].[GetEmployeeById]
                @Id INT
            AS
            BEGIN
                SET NOCOUNT ON;
                SELECT Id, Name, DepartmentId
                FROM Employees
                WHERE Id = @Id;
            END
            """);

        // SP: GetAllDepartments — no parameters
        await ExecuteSqlAsync(conn, """
            CREATE PROCEDURE [dbo].[GetAllDepartments]
            AS
            BEGIN
                SET NOCOUNT ON;
                SELECT Id, Name FROM Departments;
            END
            """);
    }

    private static async Task ExecuteSqlAsync(SqlConnection conn, string sql)
    {
        await using var cmd = new SqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>;
