using SqlToObjectify;
using SqlToObjectify.ViewModels;

using var context = new SqlObjectDbContext();
const string sqlQuery1 =
    "SELECT d.Name AS DepartmentName, COUNT(e.Id) AS NumberOfEmployees " +
    "FROM Departments d inner JOIN Employees e ON d.Id = e.DepartmentId " +
    "GROUP BY d.Name " +
    "having  COUNT(e.Id)>=@numberOfEmployees " +
    "ORDER BY  d.Name;";

            var paramList1= new Dictionary<string, object>
            {
                { "numberOfEmployees", 4 }
            };

var departmentsWithEmployeesCount = context
    .ReadDataBySqlQuery(sqlQuery1, paramList1, true)
    .MapToObjectList<DepartmentEmployeeCountViewModel>();


if (departmentsWithEmployeesCount != null)
    foreach (var department in departmentsWithEmployeesCount)
    {
        Console.WriteLine($"Name: {department.DepartmentName}, # Employees: {department.NumberOfEmployees}");
    }



const string sqlQuery2= @"select count(*) as CountEmployeeInDepartment 
                                  from Employees e
                                  where e.DepartmentId = @departmentId";
var paramList2 = new Dictionary<string, object>
{
    { "departmentId", 4 }
};

var getCountEmployeeInDepartment = context
    .ReadDataBySqlQuery(sqlQuery2, paramList2, true)
    .MapToObject<NumberOfEmployeeInDepartmentViewModel>();

if (getCountEmployeeInDepartment != null)
{
    Console.WriteLine();
    Console.WriteLine($"Number of employee in departmentId: {getCountEmployeeInDepartment.CountEmployeeInDepartment}");

}
