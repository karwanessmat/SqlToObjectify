using SqlToObjectify.Test.Fixtures;
using SqlToObjectifyLibrary;
using Xunit;

namespace SqlToObjectify.Test.Tests;

[Collection("Database")]
public class RawSqlMappingTests
{
    private readonly DatabaseFixture _fixture;

    public RawSqlMappingTests(DatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SelectList_ReturnsAllEmployees()
    {
        await using var ctx = _fixture.CreateContext();
        var result = await ctx.SelectSqlQueryListAsync<EmployeeDto>(
            "SELECT Id, Name, DepartmentId FROM Employees");
        Assert.Equal(30, result.Count);
        Assert.All(result, e => Assert.False(string.IsNullOrEmpty(e.Name)));
    }

    [Fact]
    public async Task SelectList_WithParameters_FiltersCorrectly()
    {
        await using var ctx = _fixture.CreateContext();
        var result = await ctx.SelectSqlQueryListAsync<EmployeeDto>(
            "SELECT Id, Name, DepartmentId FROM Employees WHERE DepartmentId = @deptId",
            new Dictionary<string, object> { ["deptId"] = 1 });
        Assert.Equal(6, result.Count);
        Assert.All(result, e => Assert.Equal(1, e.DepartmentId));
    }

    [Fact]
    public async Task SelectList_WithAtPrefixedParam_Works()
    {
        await using var ctx = _fixture.CreateContext();
        var result = await ctx.SelectSqlQueryListAsync<EmployeeDto>(
            "SELECT Id, Name, DepartmentId FROM Employees WHERE DepartmentId = @deptId",
            new Dictionary<string, object> { ["@deptId"] = 2 });
        Assert.Equal(6, result.Count);
        Assert.All(result, e => Assert.Equal(2, e.DepartmentId));
    }

    [Fact]
    public async Task SelectFirstOrDefault_ReturnsSingleRow()
    {
        await using var ctx = _fixture.CreateContext();
        var result = await ctx.SelectSqlQueryFirstOrDefaultAsync<EmployeeDto>(
            "SELECT TOP 1 Id, Name, DepartmentId FROM Employees WHERE Id = @id",
            new Dictionary<string, object> { ["id"] = 5 });
        Assert.NotNull(result);
        Assert.Equal(5, result.Id);
        Assert.Equal("Employee5", result.Name);
    }

    [Fact]
    public async Task SelectFirstOrDefault_NoMatch_ReturnsDefault()
    {
        await using var ctx = _fixture.CreateContext();
        var result = await ctx.SelectSqlQueryFirstOrDefaultAsync<EmployeeDto>(
            "SELECT TOP 1 Id, Name, DepartmentId FROM Employees WHERE Id = @id",
            new Dictionary<string, object> { ["id"] = 9999 });
        Assert.Null(result);
    }

    [Fact]
    public async Task SelectList_ExtraColumns_IgnoredGracefully()
    {
        // DTO has fewer properties than the query returns (extra columns should be skipped)
        await using var ctx = _fixture.CreateContext();
        var result = await ctx.SelectSqlQueryListAsync<EmployeeNameOnlyDto>(
            "SELECT Id, Name, DepartmentId FROM Employees WHERE Id <= 3");
        Assert.Equal(3, result.Count);
        Assert.All(result, e => Assert.False(string.IsNullOrEmpty(e.Name)));
    }

    [Fact]
    public async Task SelectList_MissingColumns_StayDefault()
    {
        // DTO has more properties than the query returns — missing properties keep defaults
        await using var ctx = _fixture.CreateContext();
        var result = await ctx.SelectSqlQueryListAsync<EmployeeExtendedDto>(
            "SELECT Id, Name, DepartmentId FROM Employees WHERE Id = 1");
        Assert.Single(result);
        var emp = result[0];
        Assert.Equal(1, emp.Id);
        Assert.Null(emp.Email);   // not in query result
        Assert.Equal(0, emp.Score); // not in query result, int default
    }

    [Fact]
    public async Task SelectList_IntToEnum_MapsCorrectly()
    {
        await using var ctx = _fixture.CreateContext();
        var result = await ctx.SelectSqlQueryListAsync<EmployeeWithCategoryDto>(
            "SELECT Id, Name, DepartmentId FROM Employees WHERE DepartmentId = 1");
        Assert.All(result, e => Assert.Equal(EmployeeCategory.Regular, e.DepartmentId));
    }

    [Fact]
    public async Task SelectStream_YieldsAllRows()
    {
        await using var ctx = _fixture.CreateContext();
        var count = 0;
        await foreach (var emp in ctx.SelectSqlQueryStreamAsync<EmployeeDto>(
                           "SELECT Id, Name, DepartmentId FROM Employees"))
        {
            Assert.True(emp.Id > 0);
            count++;
        }

        Assert.Equal(30, count);
    }
}
