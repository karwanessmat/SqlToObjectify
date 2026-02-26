namespace SqlToObjectify.Test;

public sealed class SqlObjectDbContextHelper
{
    public sealed class StoredProcedureRow
    {
        public string? Name { get; set; }
    }

    public async Task SelectStoredProcedureListAsync(CancellationToken cancellationToken = default)
    {
        await using var db = new SqlObjectDbContext();

        // Lists stored procedures in the current database. Adjust query/SP name as needed.
        var rows = await db.SelectSqlQueryListAsync<StoredProcedureRow>(
            "SELECT name AS Name FROM sys.procedures ORDER BY name",
            cancellationToken: cancellationToken);

        foreach (var row in rows)
            Console.WriteLine(row.Name);
    }
}

