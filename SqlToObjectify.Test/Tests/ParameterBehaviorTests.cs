using SqlToObjectify.Test.Fixtures;
using SqlToObjectifyLibrary;
using Xunit;

namespace SqlToObjectify.Test.Tests;

[Collection("Database")]
public class ParameterBehaviorTests
{
    private readonly DatabaseFixture _fixture;

    public ParameterBehaviorTests(DatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Parameters_WithoutAtPrefix_AreNormalized()
    {
        await using var ctx = _fixture.CreateContext();
        var result = await ctx.SelectSqlQueryListAsync<EmployeeDto>(
            "SELECT Id, Name, DepartmentId FROM Employees WHERE DepartmentId = @deptId",
            new Dictionary<string, object> { ["deptId"] = 3 });
        Assert.Equal(6, result.Count);
    }

    [Fact]
    public async Task Parameters_WithAtPrefix_Work()
    {
        await using var ctx = _fixture.CreateContext();
        var result = await ctx.SelectSqlQueryListAsync<EmployeeDto>(
            "SELECT Id, Name, DepartmentId FROM Employees WHERE DepartmentId = @deptId",
            new Dictionary<string, object> { ["@deptId"] = 3 });
        Assert.Equal(6, result.Count);
    }

    [Fact]
    public async Task NullParameters_Dictionary_IsNull()
    {
        await using var ctx = _fixture.CreateContext();
        var result = await ctx.SelectSqlQueryListAsync<EmployeeDto>(
            "SELECT Id, Name, DepartmentId FROM Employees", null);
        Assert.Equal(30, result.Count);
    }

    [Fact]
    public async Task EmptyParameters_Dictionary_Works()
    {
        await using var ctx = _fixture.CreateContext();
        var result = await ctx.SelectSqlQueryListAsync<EmployeeDto>(
            "SELECT Id, Name, DepartmentId FROM Employees",
            new Dictionary<string, object>());
        Assert.Equal(30, result.Count);
    }

    [Fact]
    public async Task RepeatedCalls_UpdatingParameterValues()
    {
        await using var ctx = _fixture.CreateContext();
        const string sql = "SELECT Id, Name, DepartmentId FROM Employees WHERE DepartmentId = @deptId";

        // First call with deptId = 1
        var result1 = await ctx.SelectSqlQueryListAsync<EmployeeDto>(
            sql, new Dictionary<string, object> { ["deptId"] = 1 });
        Assert.Equal(6, result1.Count);
        Assert.All(result1, e => Assert.Equal(1, e.DepartmentId));

        // Second call with deptId = 2 — command should be reused, only value updated
        var result2 = await ctx.SelectSqlQueryListAsync<EmployeeDto>(
            sql, new Dictionary<string, object> { ["deptId"] = 2 });
        Assert.Equal(6, result2.Count);
        Assert.All(result2, e => Assert.Equal(2, e.DepartmentId));

        // Third call with deptId = 999 — no results
        var result3 = await ctx.SelectSqlQueryListAsync<EmployeeDto>(
            sql, new Dictionary<string, object> { ["deptId"] = 999 });
        Assert.Empty(result3);
    }

    [Fact]
    public async Task StringParameter_UpdatesCorrectly()
    {
        await using var ctx = _fixture.CreateContext();
        const string sql = "SELECT Id, Name, DepartmentId FROM Employees WHERE Name = @name";

        var r1 = await ctx.SelectSqlQueryFirstOrDefaultAsync<EmployeeDto>(
            sql, new Dictionary<string, object> { ["name"] = "Employee1" });
        Assert.NotNull(r1);
        Assert.Equal("Employee1", r1.Name);

        var r2 = await ctx.SelectSqlQueryFirstOrDefaultAsync<EmployeeDto>(
            sql, new Dictionary<string, object> { ["name"] = "Employee20" });
        Assert.NotNull(r2);
        Assert.Equal("Employee20", r2.Name);
    }

    [Fact]
    public async Task CompiledQuery_SetParameter_ReusesCommand()
    {
        await using var ctx = _fixture.CreateContext();
        await using var compiled = ctx.CompileSqlQuery<EmployeeDto>(
            "SELECT Id, Name, DepartmentId FROM Employees WHERE DepartmentId = @deptId",
            "deptId");

        compiled.SetParameter(0, 1);
        var r1 = await compiled.ToListAsync();
        Assert.Equal(6, r1.Count);

        compiled.SetParameter(0, 3);
        var r2 = await compiled.ToListAsync();
        Assert.Equal(6, r2.Count);
        Assert.All(r2, e => Assert.Equal(3, e.DepartmentId));
    }

    [Fact]
    public async Task CompiledQuery_FirstOrDefault_Works()
    {
        await using var ctx = _fixture.CreateContext();
        await using var compiled = ctx.CompileSqlQuery<EmployeeDto>(
            "SELECT TOP 1 Id, Name, DepartmentId FROM Employees WHERE Id = @id",
            "id");

        compiled.SetParameter(0, 7);
        var result = await compiled.FirstOrDefaultAsync();
        Assert.NotNull(result);
        Assert.Equal(7, result.Id);
    }
}
