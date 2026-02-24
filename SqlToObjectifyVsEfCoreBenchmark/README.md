# SqlToObjectify vs EF Core Benchmark (10,000 seeded rows)

This project benchmarks:
- EF Core LINQ projection (AsNoTracking)
- SqlToObjectify raw SQL -> DTO mapping
- EF Core FromSqlRaw mapping (keyless)

It seeds **10,000** `People` rows on first run.

## Requirements
- **.NET SDK 9** (or newer)
- **SQL Server Express LocalDB** (Windows)
  - If you have Visual Studio installed, you usually already have LocalDB.
  - Otherwise install: "SQL Server Express LocalDB".

## Run
In this folder:

```bash
dotnet restore
dotnet run -c Release
```

## Notes
- Run **without a debugger** (Visual Studio: **Ctrl+F5**). BenchmarkDotNet results are not reliable under a debugger.
- First run will take longer because it creates the DB file and seeds 10,000 rows.
- The benchmark prints the artifacts folder path at startup.
- If you want more load, change the rows count in:
  `DatabaseSeeder.EnsureCreatedAndSeededAsync(_db, rows: 10_000);`

## What to look at
- Mean time
- Allocated memory
- Gen0/Gen1 collections
