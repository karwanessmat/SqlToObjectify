using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SqlToObjectifyVsEfCoreBenchmark;

public sealed class AppDbContext : DbContext
{
    public DbSet<Person> People => Set<Person>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Person>(b =>
        {
            b.ToTable("People");
            b.HasKey(x => x.Id);
            b.Property(x => x.FirstName).HasMaxLength(80).IsRequired();
            b.Property(x => x.LastName).HasMaxLength(80).IsRequired();
            b.Property(x => x.City).HasMaxLength(80).IsRequired();
            b.HasIndex(x => x.Age);
            b.HasIndex(x => x.City);
        });

        // Keyless DTO type for FromSqlRaw benchmark
        modelBuilder.Entity<PersonDto>(b =>
        {
            b.HasNoKey();
            b.ToView(null); // not mapped to a real view/table
        });
    }

    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlServer(ConnectionStrings.SqlServer("SqlToObjectifyVsEfCoreBenchmark"));

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
