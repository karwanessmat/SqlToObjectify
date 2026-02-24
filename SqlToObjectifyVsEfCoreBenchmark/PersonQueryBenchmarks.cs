using System.Data;
using BenchmarkDotNet.Attributes;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SqlToObjectify;

namespace SqlToObjectifyVsEfCoreBenchmark;

[MemoryDiagnoser]
[WarmupCount(2)]
[IterationCount(8)]
public class PersonQueryBenchmarks
{
    private AppDbContext _db = default!;
    private int _minAge;
    private string _city = "";
    private Dictionary<string, object> _params = default!;
    private DapperParams _dapperParams = default!;
    private CompiledSqlQuery<PersonDto> _compiledQuery = default!;

    private const string SqlToDtoQuery = @"
SELECT TOP (1000000)
    Id,
    (FirstName + ' ' + LastName) AS FullName,
    Age,
    City
FROM People
WHERE Age >= @minAge AND City = @city
ORDER BY Id";

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
        _dapperParams = new DapperParams { MinAge = _minAge, City = _city };

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

        // Create and seed once
        await DatabaseSeeder.EnsureCreatedAndSeededAsync(_db, rows: 10_000);

        // Keep connection open for the whole benchmark process so we measure mapping/query cost,
        // not connection open/close overhead.
        await _db.Database.OpenConnectionAsync();

        // Pre-compile a query that reuses DbCommand + DbParameter objects across iterations.
        _compiledQuery = _db.CompileSqlQuery<PersonDto>(SqlToDtoQuery, new[] { "minAge", "city" });

        // Prime _lastCount so the very first benchmark iteration already uses a pre-sized List<T>.
        // Without this, iteration 1 (even warmup) starts with DefaultListCapacity=256 and triggers
        // 5+ List reallocations, adding noise to the first measured iteration.
        _compiledQuery.SetParameter(0, _minAge);
        _compiledQuery.SetParameter(1, _city);
        _ = await _compiledQuery.ToListAsync();

        // Also prime the internal cache for the raw-SQL path (SelectSqlQueryListAsync).
        // First call builds the command + resolves the RowFactory.  Second call uses the hot path.
        // Without this, the first benchmark iteration of SqlToObjectify_RawSql_ToDto pays the
        // one-time CreateCommand + GetOrAdd cost, adding noise identical to compiled's first call.
        _params["minAge"] = _minAge;
        _params["city"] = _city;
        _ = await _db.SelectSqlQueryListAsync<PersonDto>(SqlToDtoQuery, _params);
        _ = await _db.SelectSqlQueryListAsync<PersonDto>(SqlToDtoQuery, _params);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _compiledQuery.DisposeAsync();
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
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
        // Database name is used unquoted in CREATE DATABASE; keep it simple.
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

    // ✅ Typical EF Core read-only query (fast if AsNoTracking + projection)
    [Benchmark(Baseline = true)]
    public async Task<List<PersonDto>> EfCore_LinqProjection_AsNoTracking()
    {
        return await _db.People
            .AsNoTracking()
            .Where(p => p.Age >= _minAge && p.City == _city)
            .OrderBy(p => p.Id)
            .Select(p => new PersonDto
            {
                Id = p.Id,
                FullName = p.FirstName + " " + p.LastName,
                Age = p.Age,
                City = p.City
            })
            .ToListAsync();
    }

    // ✅ Raw SQL through SqlToObjectify (maps columns to DTO properties)
    [Benchmark]
    public async Task<List<PersonDto>> SqlToObjectify_RawSql_ToDto()
    {
        _params["minAge"] = _minAge;
        _params["city"] = _city;
        return await _db.SelectSqlQueryListAsync<PersonDto>(SqlToDtoQuery, _params);
    }

    [Benchmark]
    public async Task<List<PersonDto>> SqlToObjectify_Compiled_RawSql_ToDto()
    {
        _compiledQuery.SetParameter(0, _minAge);
        _compiledQuery.SetParameter(1, _city);
        return await _compiledQuery.ToListAsync();
    }

    // Single-pass streaming mapper (no ExpandoObject / no "execute twice" header pass)
    [Benchmark]
    public async Task<List<PersonDto>> RawSql_FastMapper_ToDto()
    {
        _params["minAge"] = _minAge;
        _params["city"] = _city;
        return await _db.SelectSqlQueryListFastAsync<PersonDto>(SqlToDtoQuery, _params);
    }

    [Benchmark]
    public async Task<List<PersonDto>> Dapper_RawSql_ToDto()
    {
        _dapperParams.MinAge = _minAge;
        _dapperParams.City = _city;

        var connection = _db.Database.GetDbConnection();
        var rows = await connection.QueryAsync<PersonDto>(
            SqlToDtoQuery,
            _dapperParams,
            commandType: CommandType.Text);

        return rows.AsList();
    }

    // Optional: EF Core FromSqlRaw mapping to keyless type (another raw SQL path)
    [Benchmark]
    public async Task<List<PersonDto>> EfCore_FromSqlRaw_Keyless()
    {
        // Use the exact same SQL shape as SqlToObjectify_RawSql_ToDto for a fair comparison.
        // Keyless set defined via modelBuilder in AppDbContext.OnModelCreating.
        return await _db.Set<PersonDto>()
            .FromSqlRaw(
                SqlToDtoQuery,
                new SqlParameter("@minAge", _minAge),
                new SqlParameter("@city", _city))
            .AsNoTracking()
            .ToListAsync();
    }
}

internal sealed class DapperParams
{
    public int MinAge { get; set; }
    public string City { get; set; } = "";
}
