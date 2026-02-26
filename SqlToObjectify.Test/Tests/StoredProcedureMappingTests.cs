using SqlToObjectify.Test.Fixtures;
using SqlToObjectifyLibrary;
using Xunit;

namespace SqlToObjectify.Test.Tests;

[Collection("Database")]
public class StoredProcedureMappingTests
{
    private readonly DatabaseFixture _fixture;

    public StoredProcedureMappingTests(DatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SpList_ReturnsFilteredEmployees()
    {
        await using var ctx = _fixture.CreateContext();
        var result = await ctx.SelectStoredProcedureListAsync<EmployeeDto>(
            "GetEmployeesByDepartmentId",
            new Dictionary<string, object> { ["DepartmentId"] = 1 });
        Assert.Equal(6, result.Count);
        Assert.All(result, e => Assert.Equal(1, e.DepartmentId));
    }

    [Fact]
    public async Task SpList_NoDepartmentMatch_ReturnsEmpty()
    {
        await using var ctx = _fixture.CreateContext();
        var result = await ctx.SelectStoredProcedureListAsync<EmployeeDto>(
            "GetEmployeesByDepartmentId",
            new Dictionary<string, object> { ["DepartmentId"] = 999 });
        Assert.Empty(result);
    }

    [Fact]
    public async Task SpList_NoParameters_ReturnsAllDepartments()
    {
        await using var ctx = _fixture.CreateContext();
        var result = await ctx.SelectStoredProcedureListAsync<DepartmentDto>(
            "GetAllDepartments");
        Assert.Equal(5, result.Count);
    }

    [Fact]
    public async Task SpFirstOrDefault_ReturnsSingleEmployee()
    {
        await using var ctx = _fixture.CreateContext();
        var result = await ctx.SelectStoredProcedureFirstOrDefaultAsync<EmployeeDto>(
            "GetEmployeeById",
            new Dictionary<string, object> { ["Id"] = 10 });
        Assert.NotNull(result);
        Assert.Equal(10, result.Id);
        Assert.Equal("Employee10", result.Name);
    }

    [Fact]
    public async Task SpFirstOrDefault_NoMatch_ReturnsDefault()
    {
        await using var ctx = _fixture.CreateContext();
        var result = await ctx.SelectStoredProcedureFirstOrDefaultAsync<EmployeeDto>(
            "GetEmployeeById",
            new Dictionary<string, object> { ["Id"] = 9999 });
        Assert.Null(result);
    }

    [Fact]
    public async Task SpList_DepartmentEmployeeCounts_MapsAggregation()
    {
        await using var ctx = _fixture.CreateContext();
        var result = await ctx.SelectStoredProcedureListAsync<DepartmentEmployeeCountDto>(
            "GetDepartmentEmployeeCounts");
        Assert.Equal(5, result.Count);
        Assert.All(result, d =>
        {
            Assert.False(string.IsNullOrEmpty(d.DepartmentName));
            Assert.Equal(6, d.NumberOfEmployee);
        });
    }

    [Fact]
    public async Task SpStream_YieldsAllRows()
    {
        await using var ctx = _fixture.CreateContext();
        var count = 0;
        await foreach (var dept in ctx.SelectStoredProcedureStreamAsync<DepartmentDto>(
                           "GetAllDepartments"))
        {
            Assert.True(dept.Id > 0);
            count++;
        }

        Assert.Equal(5, count);
    }
}
