using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

public static class DatabaseSeeder
{
    private const string MarkerFileName = ".seeded.marker";

    public static async Task EnsureCreatedAndSeededAsync(AppDbContext db, int rows)
    {
        // Create DB + schema (via migrations). This is safer than EnsureCreated when multiple
        // benchmark processes start at the same time (and avoids "Database already exists" races).
        try
        {
            await db.Database.MigrateAsync();
        }
        catch (SqlException ex) when (ex.Number == 1801)
        {
            // "Database 'X' already exists" can happen if another process created it between the
            // existence check and CREATE DATABASE. At this point it exists, so continue.
            await db.Database.MigrateAsync();
        }

        var databaseName = db.Database.GetDbConnection().Database;
        var safeDbName = string.Concat(databaseName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        var markerPath = Path.Combine(AppContext.BaseDirectory, $"{MarkerFileName}.{safeDbName}");

        var hasData = await db.People.AnyAsync();

        // If we already seeded AND the table still has data, skip.
        // (If someone drops/recreates the DB but the marker file remains, we must re-seed.)
        if (File.Exists(markerPath) && hasData)
            return;

        // If table already has data but marker is missing, create marker and skip.
        if (hasData)
        {
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O"));
            return;
        }

        Console.WriteLine($"Seeding {rows:N0} rows into People...");

        var rng = new Random(123);

        string[] first = ["Aso", "Baran", "Karwan", "Botan", "Sara", "Narin", "Shwan", "Dana", "Rebaz", "Ranja"];
        string[] last  = ["Othman", "Ahmed", "Hassan", "Ali", "Salih", "Mahmood", "Qadir", "Fatah", "Mustafa", "Rashid"];
        string[] cities = ["Erbil", "Duhok", "Sulaymaniyah", "Kirkuk", "Baghdad", "Mosul"];

        const int batchSize = 1000;
        var batch = new List<Person>(batchSize);

        for (int i = 0; i < rows; i++)
        {
            batch.Add(new Person
            {
                FirstName = first[rng.Next(first.Length)],
                LastName  = last[rng.Next(last.Length)],
                Age       = rng.Next(18, 70),
                City      = cities[rng.Next(cities.Length)],
            });

            if (batch.Count == batchSize)
            {
                db.People.AddRange(batch);
                await db.SaveChangesAsync();
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            db.People.AddRange(batch);
            await db.SaveChangesAsync();
        }

        // Helpful indexes already in model
        File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O"));

        Console.WriteLine("Seeding done.");
    }
}
