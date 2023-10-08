using SqlToObjectify;
using SqlToObjectify.ViewModels;

using var context = new SqlObjectDbContext();
const string sqlQuery1 =
    @"SELECT d.Name AS DepartmentName, COUNT(e.Id) AS NumberOfEmployees 
    FROM Departments d inner JOIN Employees e ON d.Id = e.DepartmentId 
    GROUP BY d.Name 
    having  COUNT(e.Id)>=@numberOfEmployees 
    ORDER BY  d.Name";

            var paramList1= new Dictionary<string, object>
            {
                { "numberOfEmployees", 0 }
            };


var departmentsWithEmployeesCount = context
    .ExecuteSqlQuery(sqlQuery1, paramList1)
    .MapToObjectList<DepartmentEmployeeCountViewModel>();


if (departmentsWithEmployeesCount != null)
    foreach (var department in departmentsWithEmployeesCount)
    {
        Console.WriteLine($"Name: {department.DepartmentName}, # Employees: {department.NumberOfEmployees}");
    }
Console.WriteLine("*******************************");
Console.WriteLine();




const string sqlQuery2= @"select count(*) as CountEmployeeInDepartment 
                                  from Employees e
                                  where e.DepartmentId = @departmentId";

var paramList2 = new Dictionary<string, object>
{
    { "departmentId", 4 }
};


var getCountEmployeeInDepartment = context
    .ExecuteSqlQuery(sqlQuery2, paramList2, false)
    .MapToObject<NumberOfEmployeeInDepartmentViewModel>();


if (getCountEmployeeInDepartment != null)
{
    Console.WriteLine();
    Console.WriteLine($"Number of employee in departmentId-{paramList2["departmentId"]}: {getCountEmployeeInDepartment.CountEmployeeInDepartment}");
}
Console.WriteLine("*******************************");
Console.WriteLine();


// without parameters
const string sqlQuery3=
    @"SELECT d.Name AS DepartmentName, COUNT(e.Id) AS NumberOfEmployees 
    FROM Departments d inner JOIN Employees e ON d.Id = e.DepartmentId 
    GROUP BY d.Name 
    ORDER BY  d.Name;";




var allDepartmentsWithEmployeesCount = context
    .ExecuteSqlQuery(sqlQuery3)
    .MapToObjectList<DepartmentEmployeeCountViewModel>();


if (allDepartmentsWithEmployeesCount != null)
    foreach (var department in allDepartmentsWithEmployeesCount)
    {
        Console.WriteLine($"Name: {department.DepartmentName}, # Employees: {department.NumberOfEmployees}");
    }
Console.WriteLine("*******************************");





// "Stored Procedure"
const string getEmployeesByDepartmentIdStoredProcedure= "GetEmployeesByDepartmentId";
var spParamList = new Dictionary<string, object>
{
    { "departmentId", 4 }
};


var getEmployeesByDepartmentId = context
    .ExecuteSqlQuery(getEmployeesByDepartmentIdStoredProcedure, spParamList)
    .MapToObjectList<EmployeesByDepartmentViewModel>();



Console.WriteLine("Stored Procedure");
if (getEmployeesByDepartmentId != null)
    foreach (var department in getEmployeesByDepartmentId)
    {
        Console.WriteLine($"department Id: {department.Id}, # Employee Name: {department.Name}");
    }
Console.WriteLine("*******************************");
Console.ReadKey();

