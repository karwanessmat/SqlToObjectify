﻿// <auto-generated />
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SqlToObjectify;

#nullable disable

namespace SqlToObjectify.Migrations
{
    [DbContext(typeof(SqlObjectDbContext))]
    partial class SqlObjectDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.11")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("SqlToObjectify.Models.Department", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.HasKey("Id");

                    b.ToTable("Departments");

                    b.HasData(
                        new
                        {
                            Id = 1,
                            Name = "Department1"
                        },
                        new
                        {
                            Id = 2,
                            Name = "Department2"
                        },
                        new
                        {
                            Id = 3,
                            Name = "Department3"
                        },
                        new
                        {
                            Id = 4,
                            Name = "Department4"
                        },
                        new
                        {
                            Id = 5,
                            Name = "Department5"
                        });
                });

            modelBuilder.Entity("SqlToObjectify.Models.Employee", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<int>("DepartmentId")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.HasKey("Id");

                    b.HasIndex("DepartmentId");

                    b.ToTable("Employees");

                    b.HasData(
                        new
                        {
                            Id = 1,
                            DepartmentId = 2,
                            Name = "Employee1"
                        },
                        new
                        {
                            Id = 2,
                            DepartmentId = 3,
                            Name = "Employee2"
                        },
                        new
                        {
                            Id = 3,
                            DepartmentId = 4,
                            Name = "Employee3"
                        },
                        new
                        {
                            Id = 4,
                            DepartmentId = 5,
                            Name = "Employee4"
                        },
                        new
                        {
                            Id = 5,
                            DepartmentId = 1,
                            Name = "Employee5"
                        },
                        new
                        {
                            Id = 6,
                            DepartmentId = 2,
                            Name = "Employee6"
                        },
                        new
                        {
                            Id = 7,
                            DepartmentId = 3,
                            Name = "Employee7"
                        },
                        new
                        {
                            Id = 8,
                            DepartmentId = 4,
                            Name = "Employee8"
                        },
                        new
                        {
                            Id = 9,
                            DepartmentId = 5,
                            Name = "Employee9"
                        },
                        new
                        {
                            Id = 10,
                            DepartmentId = 1,
                            Name = "Employee10"
                        },
                        new
                        {
                            Id = 11,
                            DepartmentId = 2,
                            Name = "Employee11"
                        },
                        new
                        {
                            Id = 12,
                            DepartmentId = 3,
                            Name = "Employee12"
                        },
                        new
                        {
                            Id = 13,
                            DepartmentId = 4,
                            Name = "Employee13"
                        },
                        new
                        {
                            Id = 14,
                            DepartmentId = 5,
                            Name = "Employee14"
                        },
                        new
                        {
                            Id = 15,
                            DepartmentId = 1,
                            Name = "Employee15"
                        },
                        new
                        {
                            Id = 16,
                            DepartmentId = 2,
                            Name = "Employee16"
                        },
                        new
                        {
                            Id = 17,
                            DepartmentId = 3,
                            Name = "Employee17"
                        },
                        new
                        {
                            Id = 18,
                            DepartmentId = 4,
                            Name = "Employee18"
                        },
                        new
                        {
                            Id = 19,
                            DepartmentId = 5,
                            Name = "Employee19"
                        },
                        new
                        {
                            Id = 20,
                            DepartmentId = 1,
                            Name = "Employee20"
                        },
                        new
                        {
                            Id = 21,
                            DepartmentId = 2,
                            Name = "Employee21"
                        },
                        new
                        {
                            Id = 22,
                            DepartmentId = 3,
                            Name = "Employee22"
                        },
                        new
                        {
                            Id = 23,
                            DepartmentId = 4,
                            Name = "Employee23"
                        },
                        new
                        {
                            Id = 24,
                            DepartmentId = 5,
                            Name = "Employee24"
                        },
                        new
                        {
                            Id = 25,
                            DepartmentId = 1,
                            Name = "Employee25"
                        },
                        new
                        {
                            Id = 26,
                            DepartmentId = 2,
                            Name = "Employee26"
                        },
                        new
                        {
                            Id = 27,
                            DepartmentId = 3,
                            Name = "Employee27"
                        },
                        new
                        {
                            Id = 28,
                            DepartmentId = 4,
                            Name = "Employee28"
                        },
                        new
                        {
                            Id = 29,
                            DepartmentId = 5,
                            Name = "Employee29"
                        },
                        new
                        {
                            Id = 30,
                            DepartmentId = 1,
                            Name = "Employee30"
                        });
                });

            modelBuilder.Entity("SqlToObjectify.Models.Employee", b =>
                {
                    b.HasOne("SqlToObjectify.Models.Department", "Department")
                        .WithMany()
                        .HasForeignKey("DepartmentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Department");
                });
#pragma warning restore 612, 618
        }
    }
}