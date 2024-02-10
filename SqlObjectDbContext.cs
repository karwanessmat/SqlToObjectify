using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SqlToObjectify.Models;

namespace SqlToObjectify
{
    public class SqlObjectDbContext:DbContext
    {

        public SqlObjectDbContext()
        
        {
        }
        public SqlObjectDbContext(DbContextOptions<SqlObjectDbContext> options) 
            : base(options)
        {
        }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Position> Position { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {

            const string connectionStrings = "Server=.;Database=SqlObjectDb;Integrated Security=True;Encrypt=False;";

            optionsBuilder.UseSqlServer(connectionStrings);
            base.OnConfiguring(optionsBuilder);

        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<Department>(ConfigureDepartment);
            builder.Entity<Employee>(ConfigureEmployee);


            // Seeding departments
            var departments = new List<Department>
            {
                new() { Id = 1, Name = "Department1" },
                new() { Id = 2, Name = "Department2" },
                new() { Id = 3, Name = "Department3" },
                new() { Id = 4, Name = "Department4" },
                new() { Id = 5, Name = "Department5" }
            };
            builder.Entity<Department>().HasData(departments);


            // Seeding employees
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


    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SqlObjectDbContext>
    {
        public SqlObjectDbContext CreateDbContext(string[] args)
        {

            var builder = new DbContextOptionsBuilder<SqlObjectDbContext>();

            const string connectionStrings = "Server=.;Database=SqlObjectDb;Integrated Security=True;Encrypt=False;";
            builder.UseSqlServer(connectionStrings);
            return new SqlObjectDbContext(builder.Options);
        }
    }



}
