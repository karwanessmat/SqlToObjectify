using SqlToObjectify.ViewModels;

namespace SqlToObjectify
{
    internal class SqlObjectDbContextHelper()
    {
        private readonly SqlObjectDbContext dbContext = new SqlObjectDbContext();
        public async Task SelectSqlQueryListAsync()
        {

            const string selectSqlQueryListAsync =
             @"SELECT d.Name AS DepartmentName, COUNT(e.Id) AS NumberOfEmployee 
            FROM Departments d inner JOIN Employees e ON d.Id = e.DepartmentId 
            GROUP BY d.Name 
            having  COUNT(e.Id)>=@numberOfEmployees 
            ORDER BY  d.Name";

            var paramList1 = new Dictionary<string, object>
            {
                { "numberOfEmployees", 1 }
            };


            var result = await dbContext
                .SelectSqlQueryListAsync<DepartmentEmployeeCountViewModel>(selectSqlQueryListAsync, paramList1);


            foreach (var department in result)
            {
                Console.WriteLine($"Name: {department.DepartmentName}, # Employees: {department.NumberOfEmployee}");
            }
            Console.WriteLine("*******************************");
            Console.WriteLine();
        }

        private async Task SelectSqlQueryFirstOrDefaultAsync()
        {
            const string selectSqlQueryAsync = @"select count(*) as CountEmployeeInDepartment 
                                  from Employees e
                                  where e.DepartmentId = @departmentId";

            var paramList2 = new Dictionary<string, object>
            {
                { "departmentId", 1 }
            };


            var result = await dbContext
                .SelectSqlQueryFirstOrDefaultAsync<NumberOfEmployeeInDepartmentViewModel>(selectSqlQueryAsync, paramList2);
         
  
                Console.WriteLine($"Number Of Employee In Department : {result.CountEmployeeInDepartment}");
            Console.WriteLine("*******************************");
            Console.WriteLine();
        }

        private async Task ExecuteSqlQueryCommandAsync()
        {

            var updateEmployeeFromExecuteQuery = "update Employees set Name =Name +' - update' where id = @employeeId";

            var updateParamList = new Dictionary<string, object>
            {
                { "employeeId", 1 }
            };



            await dbContext
                .ExecuteSqlQueryCommandAsync(updateEmployeeFromExecuteQuery, updateParamList);



            Console.WriteLine("*******************************");
            Console.WriteLine();
        }

        private async Task SelectStoredProcedureListAsync()
        {

            // "Stored Procedure"
            const string getEmployeesByDepartmentIdStoredProcedure = "GetEmployeesByDepartmentId";
            var spParamList = new Dictionary<string, object>
            {
                { "departmentId", 4 }
            };


            var result = await dbContext
                .SelectStoredProcedureListAsync<EmployeesByDepartmentViewModel>(getEmployeesByDepartmentIdStoredProcedure, spParamList);



            Console.WriteLine("Stored Procedure");
            foreach (var department in result)
            {
                Console.WriteLine($"department Id: {department.Id}, # Employee Name: {department.Name}");
            }
            Console.WriteLine("*******************************");
        }

        private async Task SelectStoredProcedureFirstOrDefaultAsync()
        {
            const string getEmployeesByDepartmentIdStoredProcedure = "GetEmployeesByDepartmentId";
            var spParamList = new Dictionary<string, object>
            {
                { "departmentId", 4 }
            };


            var result= await dbContext
                .SelectStoredProcedureFirstOrDefaultAsync<EmployeesByDepartmentViewModel>(getEmployeesByDepartmentIdStoredProcedure, spParamList);
            Console.WriteLine($"select an object by Stored procedure");

            Console.WriteLine("*******************************");
        }

        private async Task ExecuteStoredProcedureAsync()
        {

            const string updateEmployeeNameStoredProcedure = "updateEmployeeName";
            var spUpdateParamList = new Dictionary<string, object>
            {
                { "employeeId", 4 },
                { "name", "update name" }
            };


            await dbContext
                .ExecuteStoredProcedureAsync(updateEmployeeNameStoredProcedure, spUpdateParamList);

            Console.WriteLine("*******************************");
            Console.ReadKey();



        }
 
    }
}
