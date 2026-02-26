using System.Data;
using BenchmarkDotNet.Attributes;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SqlToObjectifyLibrary;

namespace SqlToObjectifyVsEfCoreBenchmark;

/// <summary>
/// Benchmarks stored-procedure execution: SqlToObjectify vs Dapper vs EF Core FromSqlRaw.
/// Measures both "cold" (first call, uncached) and "hot" (repeated, cached) paths.
/// </summary>
[MemoryDiagnoser]
[WarmupCount(2)]
[IterationCount(8)]
public class StoredProcedureBenchmarks
{
    private AppDbContext _db = default!;
    private int _minAge;
    private string _city = "";
    private Dictionary<string, object> _params = default!;
    private DapperSpParams _dapperParams = default!;
    private CompiledSqlQuery<PersonDto> _compiledSp = default!;

    private const string SpName = "GetPeopleByAgeAndCity";

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        _minAge = 40;
        _city = "Erbil";
        _params = new Dictionary<string, object>
        {
            ["minAge"] = _minAge,
            ["city"] = _city
        };
        _dapperParams = new DapperSpParams { MinAge = _minAge, City = _city };

        var dbName =
            GetCommandLineArgValue("--dbName") ??
            "SqlToObjectifyVsEfCoreBenchmark";

        var cs = ConnectionStrings.SqlServer(SanitizeDbName(dbName));

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(cs)
            .EnableSensitiveDataLogging(false)
            .EnableDetailedErrors(false)
            .Options;

        _db = new AppDbContext(options);

        await DatabaseSeeder.EnsureCreatedAndSeededAsync(_db, rows: 10_000);

        // Create the stored procedure if it doesn't exist
        await EnsureStoredProcedureAsync(cs);

        // Keep connection open for the whole benchmark
        await _db.Database.OpenConnectionAsync();

        // Pre-compile a stored procedure query
        _compiledSp = _db.CompileStoredProcedure<PersonDto>(SpName, "minAge", "city");

        // Prime caches: warm up compiled path
        _compiledSp.SetParameter(0, _minAge);
        _compiledSp.SetParameter(1, _city);
        _ = await _compiledSp.ToListAsync();

        // Prime the internal cache for the dictionary-API stored procedure path
        _ = await _db.SelectStoredProcedureListAsync<PersonDto>(SpName, _params);
        _ = await _db.SelectStoredProcedureListAsync<PersonDto>(SpName, _params);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _compiledSp.DisposeAsync();
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // ---- Benchmarks ----

    /// <summary>
    /// SqlToObjectify stored procedure with dictionary params (hot path — command + factory cached).
    /// </summary>
    [Benchmark(Baseline = true)]
    public async Task<List<PersonDto>> SqlToObjectify_StoredProc_ToDto()
    {
        _params["minAge"] = _minAge;
        _params["city"] = _city;
        return await _db.SelectStoredProcedureListAsync<PersonDto>(SpName, _params);
    }

    /// <summary>
    /// SqlToObjectify pre-compiled stored procedure (hot path — zero dictionary, ordinal SetParameter).
    /// </summary>
    [Benchmark]
    public async Task<List<PersonDto>> SqlToObjectify_Compiled_StoredProc_ToDto()
    {
        _compiledSp.SetParameter(0, _minAge);
        _compiledSp.SetParameter(1, _city);
        return await _compiledSp.ToListAsync();
    }

    /// <summary>
    /// Dapper stored procedure execution.
    /// </summary>
    [Benchmark]
    public async Task<List<PersonDto>> Dapper_StoredProc_ToDto()
    {
        _dapperParams.MinAge = _minAge;
        _dapperParams.City = _city;

        var connection = _db.Database.GetDbConnection();
        var rows = await connection.QueryAsync<PersonDto>(
            SpName,
            _dapperParams,
            commandType: CommandType.StoredProcedure);

        return rows.AsList();
    }

    /// <summary>
    /// EF Core FromSqlRaw with stored procedure EXEC (keyless entity).
    /// </summary>
    [Benchmark]
    public async Task<List<PersonDto>> EfCore_FromSqlRaw_StoredProc()
    {
        return await _db.Set<PersonDto>()
            .FromSqlRaw(
                $"EXEC {SpName} @minAge, @city",
                new SqlParameter("@minAge", _minAge),
                new SqlParameter("@city", _city))
            .AsNoTracking()
            .ToListAsync();
    }

    // ---- Helpers ----

    private static async Task EnsureStoredProcedureAsync(string connectionString)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        // Drop + recreate to ensure the SP matches the current schema.
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            IF OBJECT_ID('{SpName}', 'P') IS NOT NULL
                DROP PROCEDURE [{SpName}];
            """;
        await cmd.ExecuteNonQueryAsync();

        cmd.CommandText = $"""
            CREATE PROCEDURE [{SpName}]
                @minAge INT,
                @city   NVARCHAR(80)
            AS
            BEGIN
                SET NOCOUNT ON;
                SELECT TOP (1000000)
                    Id,
                    (FirstName + ' ' + LastName) AS FullName,
                    Age,
                    City
                FROM People
                WHERE Age >= @minAge AND City = @city
                ORDER BY Id;
            END
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    private static string? GetCommandLineArgValue(string name)
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private static string SanitizeDbName(string value)
    {
        Span<char> buffer = stackalloc char[Math.Min(96, value.Length)];
        var written = 0;
        foreach (var ch in value)
        {
            if (written == buffer.Length)
                break;
            buffer[written++] = char.IsLetterOrDigit(ch) ? ch : '_';
        }

        return written == 0 ? "Default" : new string(buffer[..written]);
    }
}

internal sealed class DapperSpParams
{
    public int MinAge { get; set; }
    public string City { get; set; } = "";
}
