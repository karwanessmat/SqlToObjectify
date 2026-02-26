# SqlToObjectify

Execute raw SQL queries or stored procedures directly from your `DbContext` and map results to strongly-typed objects — **faster and lighter than Dapper**.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://choosealicense.com/licenses/mit/)

## Install

```bash
dotnet add package SqlToObjectify
```

## Why SqlToObjectify?

- **Zero reflection on the hot path** — expression-tree-compiled row factories, built once, reused forever
- **Command + parameter caching** — repeated calls reuse the same `DbCommand`; only `.Value` is updated
- **Typed `SqlParameter` fast-path** — no boxing for `int`, `string`, `Guid`, `DateTime`, etc.
- **Adaptive `List<T>` pre-sizing** — capacity hint from the previous call eliminates mid-iteration reallocations
- **Dual reader optimization** — `SqlDataReader`-specific compiled factory when running on SQL Server
- **Streaming support** — `IAsyncEnumerable<T>` for large result sets without buffering
- **Pre-compiled queries** — `CompiledSqlQuery<T>` for maximum throughput on hot queries

## Quick Start

```csharp
// 1. Raw SQL → List<T>
var books = await dbContext.SelectSqlQueryListAsync<BookDto>(
    "SELECT Title, Author, Price FROM Books WHERE Tag = @tag AND Price < @limit",
    new Dictionary<string, object>
    {
        ["tag"] = "Science",
        ["limit"] = 25
    });

// 2. Stored Procedure → List<T>
var employees = await dbContext.SelectStoredProcedureListAsync<EmployeeDto>(
    "GetEmployeesByDepartment",
    new Dictionary<string, object> { ["DepartmentId"] = 5 });

// 3. Single row
var book = await dbContext.SelectSqlQueryFirstOrDefaultAsync<BookDto>(
    "SELECT TOP 1 Title, Author FROM Books WHERE Id = @id",
    new Dictionary<string, object> { ["id"] = 42 });

// 4. Streaming (IAsyncEnumerable — no buffering)
await foreach (var row in dbContext.SelectSqlQueryStreamAsync<BookDto>(
    "SELECT Title, Author, Price FROM Books"))
{
    Process(row);
}

// 5. Pre-compiled query (fastest repeated path)
await using var compiled = dbContext.CompileSqlQuery<BookDto>(
    "SELECT Title, Author, Price FROM Books WHERE Tag = @tag", "tag");

compiled.SetParameter(0, "Science");
var result = await compiled.ToListAsync();
```

## API Reference

### SQL Queries

| Method | Returns | Description |
|---|---|---|
| `SelectSqlQueryListAsync<T>(sql, params?)` | `Task<List<T>>` | Execute query, map all rows |
| `SelectSqlQueryFirstOrDefaultAsync<T>(sql, params?)` | `Task<T>` | Execute query, map first row |
| `SelectSqlQueryStreamAsync<T>(sql, params?)` | `IAsyncEnumerable<T>` | Stream rows without buffering |
| `ExecuteSqlQueryCommandAsync(sql, params?)` | `Task` | Execute non-query (INSERT/UPDATE/DELETE) |
| `CompileSqlQuery<T>(sql, paramNames)` | `CompiledSqlQuery<T>` | Pre-compile for repeated execution |

### Stored Procedures

| Method | Returns | Description |
|---|---|---|
| `SelectStoredProcedureListAsync<T>(sp, params?)` | `Task<List<T>>` | Execute SP, map all rows |
| `SelectStoredProcedureFirstOrDefaultAsync<T>(sp, params?)` | `Task<T>` | Execute SP, map first row |
| `SelectStoredProcedureStreamAsync<T>(sp, params?)` | `IAsyncEnumerable<T>` | Stream SP rows without buffering |
| `ExecuteStoredProcedureAsync(sp, params?)` | `Task` | Execute SP non-query |
| `CompileStoredProcedure<T>(sp, paramNames)` | `CompiledSqlQuery<T>` | Pre-compile SP for repeated execution |

All methods are extension methods on `DbContext`. Parameters are passed as `Dictionary<string, object>` — keys can include or omit the `@` prefix.

## Supported Type Mappings

| Source (SQL) | Target (.NET) | Notes |
|---|---|---|
| `int`, `bigint`, `smallint`, `tinyint` | `int`, `long`, `short`, `byte` | Typed getters, zero boxing |
| `bit` | `bool` | |
| `nvarchar`, `varchar` | `string` | Size-bucketed (4000 / MAX) |
| `uniqueidentifier` | `Guid` | Also parses from `string` |
| `datetime2`, `datetime` | `DateTime` | |
| `datetimeoffset` | `DateTimeOffset` | Via `GetFieldValue<T>` |
| `date` | `DateOnly` | Converted from `DateTime` |
| `time` | `TimeOnly` | Converted from `TimeSpan` |
| `decimal`, `money` | `decimal` | |
| `float` | `double` | |
| `real` | `float` | |
| `varbinary` | `byte[]` | Size-bucketed (8000 / MAX) |
| `int` / `string` | `enum` | Parsed or cast automatically |
| Any nullable column | `T?` | `DBNull` mapped to `default` |

Extra columns in the result set are ignored. Missing columns leave the DTO property at its default value.

## Performance Model

**First call** (per query + connection + DTO type):
- Creates `DbCommand` with typed `SqlParameter` objects
- Compiles an expression-tree row factory (one for `DbDataReader`, one for `SqlDataReader`)
- Caches everything in a `ConcurrentDictionary`

**Every subsequent call** (hot path):
- Reuses cached `DbCommand` — only updates `.Value` on existing parameters (ordinal-indexed, zero string ops)
- Skips factory lookup entirely — factory stored in the cache entry
- Pre-sizes `List<T>` from the previous call's row count

The hot path allocates only the `List<T>`, the `T` instances, and the async state machine. No reflection, no parameter creation, no dictionary lookups.

## Parameters

```csharp
// Both forms work — the library normalizes automatically:
new Dictionary<string, object> { ["tag"] = "Science" }      // without @
new Dictionary<string, object> { ["@tag"] = "Science" }     // with @

// Null parameters:
new Dictionary<string, object> { ["tag"] = DBNull.Value }   // explicit DBNull
// or simply omit the parameter dictionary for queries without parameters
```

## Requirements

- **.NET 10.0+**
- **Entity Framework Core 10.0+** (Relational)
- **Microsoft.Data.SqlClient 6.0+** (for SQL Server fast-path; general `DbDataReader` path works with any provider)

## License

[MIT](https://choosealicense.com/licenses/mit/)

## Links

- [GitHub](https://github.com/karwanessmat/SqlToObjectify)
- [NuGet](https://www.nuget.org/packages/SqlToObjectify)
