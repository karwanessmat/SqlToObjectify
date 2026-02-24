public static class ConnectionStrings
{
    /// <summary>
    /// Uses SQL Server LocalDB (Windows).
    /// If you don't have LocalDB installed, install SQL Server Express LocalDB.
    /// </summary>
    public static string LocalDb(string? databaseName = null)
    {
        var envDbName = Environment.GetEnvironmentVariable("BENCH_DB_NAME");
        var dbName =
            !string.IsNullOrWhiteSpace(databaseName) ? databaseName! :
            !string.IsNullOrWhiteSpace(envDbName) ? envDbName! :
            "SqlToObjectifyVsEfCoreBenchmark";

        // Data Source=(LocalDB)\MSSQLLocalDB;
        // Initial Catalog=...;Integrated Security=True;Connect Timeout=30;
        return $"Data Source=(LocalDB)\\MSSQLLocalDB;Initial Catalog={dbName};Integrated Security=True;Connect Timeout=30;TrustServerCertificate=True;";
    }

    /// <summary>
    /// Uses a full SQL Server instance (not LocalDB), using the default local instance (server=.; trusted_connection=true).
    /// </summary>
    public static string SqlServer(string? databaseName = null)
    {
        var dbName = string.IsNullOrWhiteSpace(databaseName) ? "SqlToObjectifyVsEfCoreBenchmark" : databaseName!;
        return $"server=.;database={dbName};trusted_connection=true;MultipleActiveResultSets=true;TrustServerCertificate=True;";
    }
}
