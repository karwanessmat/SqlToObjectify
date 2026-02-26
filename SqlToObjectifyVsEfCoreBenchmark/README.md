# SqlToObjectify vs EF Core / Dapper Benchmark

Benchmarks both **raw SQL** and **stored procedure** execution paths.

## Benchmark Classes

| Class | Focus |
|---|---|
| `PersonQueryBenchmarks` | Raw SQL query → DTO mapping |
| `StoredProcedureBenchmarks` | Stored procedure → DTO mapping |

Each class compares SqlToObjectify (dictionary API + compiled), Dapper, and EF Core.
Both "cold" (first call) and "hot" (repeated, cached) behavior is captured — caches are primed in `GlobalSetup` so measured iterations show the steady-state hot path.

## Requirements

- **.NET SDK 10** (or newer)
- **SQL Server** (Express or full instance on `server=.`)
  - The benchmark uses `server=.;database=SqlToObjectifyVsEfCoreBenchmark;trusted_connection=true` by default.
  - To use LocalDB instead, change `ConnectionStrings.SqlServer(...)` to `ConnectionStrings.LocalDb(...)` in both benchmark classes.

## How to Run

```bash
# All benchmarks (raw SQL + stored procedures):
dotnet run -c Release

# Only stored procedure benchmarks:
dotnet run -c Release -- --filter *StoredProcedure*

# Only raw SQL benchmarks:
dotnet run -c Release -- --filter *PersonQuery*
```

## Notes

- Run **without a debugger** (Visual Studio: Ctrl+F5). BenchmarkDotNet results are unreliable under a debugger.
- First run creates the DB and seeds 10,000 rows. Stored procedures are created automatically.
- The benchmark prints the artifacts folder path at startup.
- Connection stays open during the entire run to measure mapping cost, not connection overhead.

## What to Look At

| Metric | Meaning |
|---|---|
| **Mean** | Average execution time per iteration |
| **Allocated** | Total managed heap bytes allocated per call |
| **Gen0 / Gen1** | GC collections triggered per 1000 operations |

Lower is better for all metrics.
