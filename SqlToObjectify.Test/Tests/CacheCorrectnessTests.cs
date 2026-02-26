using SqlToObjectify.Test.Fixtures;
using SqlToObjectifyLibrary;
using Xunit;

namespace SqlToObjectify.Test.Tests;

[Collection("Database")]
public class CacheCorrectnessTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task SameQuery_SameSchema_HitsCache()
    {
        await using var ctx = fixture.CreateContext();
        const string sql = "SELECT Id, Name, DepartmentId FROM Employees WHERE DepartmentId = @d";

        // Two calls with same query text and same schema should return correct results
        // (i.e., the factory cache doesn't return stale data).
        var r1 = await ctx.SelectSqlQueryListAsync<EmployeeDto>(
            sql, new Dictionary<string, object> { ["d"] = 1 });
        var r2 = await ctx.SelectSqlQueryListAsync<EmployeeDto>(
            sql, new Dictionary<string, object> { ["d"] = 2 });

        Assert.Equal(6, r1.Count);
        Assert.All(r1, e => Assert.Equal(1, e.DepartmentId));

        Assert.Equal(6, r2.Count);
        Assert.All(r2, e => Assert.Equal(2, e.DepartmentId));
    }

    [Fact]
    public async Task DifferentQuery_SameDto_NoCollision()
    {
        await using var ctx = fixture.CreateContext();

        // Query 1: all employees
        var all = await ctx.SelectSqlQueryListAsync<EmployeeDto>(
            "SELECT Id, Name, DepartmentId FROM Employees");
        Assert.Equal(30, all.Count);

        // Query 2: filtered employees — different SQL text, same DTO type.
        // Should get its own cache entry and not collide.
        var filtered = await ctx.SelectSqlQueryListAsync<EmployeeDto>(
            "SELECT Id, Name, DepartmentId FROM Employees WHERE Id <= 5");
        Assert.Equal(5, filtered.Count);

        // Query 1 again — must still return 30
        var allAgain = await ctx.SelectSqlQueryListAsync<EmployeeDto>(
            "SELECT Id, Name, DepartmentId FROM Employees");
        Assert.Equal(30, allAgain.Count);
    }

    [Fact]
    public async Task DifferentSchema_SameQuery_Diverges()
    {
        // Two DTOs with different property sets mapping from the same raw SQL.
        // The RowFactory cache must build distinct factories for each DTO type,
        // since the schema fingerprint is the same but T is different.
        await using var ctx = fixture.CreateContext();
        const string sql = "SELECT Id, Name, DepartmentId FROM Employees WHERE Id = 1";

        var full = await ctx.SelectSqlQueryFirstOrDefaultAsync<EmployeeDto>(sql);
        var nameOnly = await ctx.SelectSqlQueryFirstOrDefaultAsync<EmployeeNameOnlyDto>(sql);

        Assert.NotNull(full);
        Assert.Equal(1, full.Id);
        Assert.Equal("Employee1", full.Name);

        Assert.NotNull(nameOnly);
        Assert.Equal("Employee1", nameOnly.Name);
    }

    [Fact]
    public async Task ListPreSizing_AdaptsToRowCount()
    {
        await using var ctx = fixture.CreateContext();
        const string sql = "SELECT Id, Name, DepartmentId FROM Employees";

        // First call establishes count
        var r1 = await ctx.SelectSqlQueryListAsync<EmployeeDto>(sql);
        Assert.Equal(30, r1.Count);

        // Second call should pre-size the list to ~30 (we can't assert capacity directly,
        // but we verify it still works correctly and returns the right count).
        var r2 = await ctx.SelectSqlQueryListAsync<EmployeeDto>(sql);
        Assert.Equal(30, r2.Count);
    }

    [Fact]
    public async Task Concurrent_SameQuery_ThreadSafe()
    {
        // Run multiple concurrent queries for the same SQL to stress the cache.
        await using var ctx = fixture.CreateContext();
        const string sql = "SELECT Id, Name, DepartmentId FROM Employees";

        var tasks = Enumerable.Range(0, 10)
            .Select(async _ =>
            {
                await using var c = fixture.CreateContext();
                return await c.SelectSqlQueryListAsync<EmployeeDto>(sql);
            })
            .ToArray();

        var results = await Task.WhenAll(tasks);
        Assert.All(results, r => Assert.Equal(30, r.Count));
    }
}
