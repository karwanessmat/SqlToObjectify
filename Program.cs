using SqlToObjectify;
using SqlToObjectify.ViewModels;

await using var context = new SqlObjectDbContext();



const string selectSqlQueryListAsync =
    @"SELECT d.Name AS DepartmentName, COUNT(e.Id) AS NumberOfEmployees 
    FROM Departments d inner JOIN Employees e ON d.Id = e.DepartmentId 
    GROUP BY d.Name 
    having  COUNT(e.Id)>=@numberOfEmployees 
    ORDER BY  d.Name";

            var paramList1= new Dictionary<string, object>
            {
                { "numberOfEmployees", 0 }
            };


var departmentsWithEmployeesCount = await context
    .SelectSqlQueryListAsync<DepartmentEmployeeCountViewModel>(selectSqlQueryListAsync, paramList1);


foreach (var department in departmentsWithEmployeesCount)
{
    Console.WriteLine($"Name: {department.DepartmentName}, # Employees: {department.NumberOfEmployee}");
}
Console.WriteLine("*******************************");
Console.WriteLine();




const string selectSqlQueryAsync = @"select count(*) as CountEmployeeInDepartment 
                                  from Employees e
                                  where e.DepartmentId = @departmentId";

var paramList2 = new Dictionary<string, object>
{
    { "departmentId", 1 }
};


var getCountEmployeeInDepartment =await context
    .SelectSqlQueryFirstOrDefaultAsync<NumberOfEmployeeInDepartmentViewModel>(selectSqlQueryAsync, paramList2);








// without parameters
const string parameterLessSelectSqlQueryListAsync =
    @"SELECT d.Name AS DepartmentName, COUNT(e.Id) AS NumberOfEmployees 
    FROM Departments d inner JOIN Employees e ON d.Id = e.DepartmentId 
    GROUP BY d.Name 
    ORDER BY  d.Name;";




var allDepartmentsWithEmployeesCount =await context
    .SelectSqlQueryListAsync<DepartmentEmployeeCountViewModel>(parameterLessSelectSqlQueryListAsync);


foreach (var department in allDepartmentsWithEmployeesCount)
{
    Console.WriteLine($"Name: {department.DepartmentName}, # Employees: {department.NumberOfEmployee}");
}
Console.WriteLine("*******************************");
Console.WriteLine();


// without parameters
const string parameterLessSelectSqlQueryAsync =
    @" SELECT  top 1 d.Name AS DepartmentName, COUNT(e.Id) AS NumberOfEmployees 
    FROM Departments d inner JOIN Employees e ON d.Id = e.DepartmentId 
    GROUP BY d.Name 
    ORDER BY  d.Name;";




var top1departmentsWithEmployeesCount = await context
    .SelectSqlQueryFirstOrDefaultAsync<DepartmentEmployeeCountViewModel>(parameterLessSelectSqlQueryAsync);

Console.WriteLine($"First department name: {top1departmentsWithEmployeesCount.DepartmentName}");

Console.WriteLine("*******************************");
Console.WriteLine();






var updateEmployeeFromExecuteQuery = "update Employees set Name =Name +' - update' where id = @employeeId";

var updateParamList = new Dictionary<string, object>
{
    { "employeeId", 1 }
};



 await context
    .ExecuteSqlQueryCommandAsync(updateEmployeeFromExecuteQuery, updateParamList);

Console.WriteLine($"First department name: {top1departmentsWithEmployeesCount.DepartmentName}");

Console.WriteLine("*******************************");
Console.WriteLine();


return;


// "Stored Procedure"
const string getEmployeesByDepartmentIdStoredProcedure= "GetEmployeesByDepartmentId";
var spParamList = new Dictionary<string, object>
{
    { "departmentId", 4 }
};


var getEmployeesByDepartmentId = await context
    .SelectSqlQueryListAsync<EmployeesByDepartmentViewModel>(getEmployeesByDepartmentIdStoredProcedure, spParamList);



Console.WriteLine("Stored Procedure");
foreach (var department in getEmployeesByDepartmentId)
{
    Console.WriteLine($"department Id: {department.Id}, # Employee Name: {department.Name}");
}
Console.WriteLine("*******************************");


// "Stored Procedure"
const string updateEmployeeNameStoredProcedure = "updateEmployeeName";
var spUpdateParamList = new Dictionary<string, object>
{
    { "employeeId", 4 },
    { "name", "update name" }
};


await context
    .ExecuteStoredProcedureAsync(updateEmployeeNameStoredProcedure, spUpdateParamList);

Console.WriteLine("*******************************");
Console.ReadKey();






