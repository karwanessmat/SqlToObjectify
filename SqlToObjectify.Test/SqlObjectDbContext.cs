using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SqlToObjectify.Test.Models;

namespace SqlToObjectify.Test;

public class SqlObjectDbContext : DbContext
{
    public SqlObjectDbContext() { }

    public SqlObjectDbContext(DbContextOptions<SqlObjectDbContext> options)
        : base(options) { }

    public DbSet<Department> Departments { get; set; }
    public DbSet<Employee> Employees { get; set; }
    public DbSet<Position> Position { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer(
                "Server=(localdb)\\MSSQLLocalDB;Database=SqlToObjectifyTestDb;Integrated Security=True;Encrypt=False;");
        }

        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Department>(ConfigureDepartment);
        builder.Entity<Employee>(ConfigureEmployee);

        // Seed departments
        var departments = new List<Department>
        {
            new() { Id = 1, Name = "Engineering" },
            new() { Id = 2, Name = "Marketing" },
            new() { Id = 3, Name = "Sales" },
            new() { Id = 4, Name = "HR" },
            new() { Id = 5, Name = "Finance" }
        };
        builder.Entity<Department>().HasData(departments);

        // Seed employees
        var employees = new List<Employee>();
        for (var i = 1; i <= 30; i++)
        {
            employees.Add(new Employee
            {
                Id = i,
                Name = $"Employee{i}",
                DepartmentId = (i % 5) + 1
            });
        }

        builder.Entity<Employee>().HasData(employees);
        base.OnModelCreating(builder);
    }

    private static void ConfigureDepartment(EntityTypeBuilder<Department> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).IsRequired().HasMaxLength(100);
    }

    private static void ConfigureEmployee(EntityTypeBuilder<Employee> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.HasOne(e => e.Department)
            .WithMany()
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
