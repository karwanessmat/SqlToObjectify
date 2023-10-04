using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SqlToObjectify.Migrations
{
    /// <inheritdoc />
    public partial class DbInit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Employees_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Department1" },
                    { 2, "Department2" },
                    { 3, "Department3" },
                    { 4, "Department4" },
                    { 5, "Department5" }
                });

            migrationBuilder.InsertData(
                table: "Employees",
                columns: new[] { "Id", "DepartmentId", "Name" },
                values: new object[,]
                {
                    { 1, 2, "Employee1" },
                    { 2, 3, "Employee2" },
                    { 3, 4, "Employee3" },
                    { 4, 5, "Employee4" },
                    { 5, 1, "Employee5" },
                    { 6, 2, "Employee6" },
                    { 7, 3, "Employee7" },
                    { 8, 4, "Employee8" },
                    { 9, 5, "Employee9" },
                    { 10, 1, "Employee10" },
                    { 11, 2, "Employee11" },
                    { 12, 3, "Employee12" },
                    { 13, 4, "Employee13" },
                    { 14, 5, "Employee14" },
                    { 15, 1, "Employee15" },
                    { 16, 2, "Employee16" },
                    { 17, 3, "Employee17" },
                    { 18, 4, "Employee18" },
                    { 19, 5, "Employee19" },
                    { 20, 1, "Employee20" },
                    { 21, 2, "Employee21" },
                    { 22, 3, "Employee22" },
                    { 23, 4, "Employee23" },
                    { 24, 5, "Employee24" },
                    { 25, 1, "Employee25" },
                    { 26, 2, "Employee26" },
                    { 27, 3, "Employee27" },
                    { 28, 4, "Employee28" },
                    { 29, 5, "Employee29" },
                    { 30, 1, "Employee30" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_DepartmentId",
                table: "Employees",
                column: "DepartmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropTable(
                name: "Departments");
        }
    }
}
