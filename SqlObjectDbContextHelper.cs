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

        public async Task SelectStoredProcedureListAsync()
        {
            // 
            var sp = """
                     create or alter procedure SelectAllDepartment
                     @searchQuery nvarchar(max)
                     as
                     begin
                     	select  *
                     	from  Departments as d
                     	where d.Name like '%'+@searchQuery+'%'
                     end
                     """;

            await dbContext
                .ExecuteSqlQueryCommandAsync(sp);

            var spParamList = new Dictionary<string, object>
            {
                { "searchQuery", "department5" }
            };

            // "Stored Procedure"
            const string getEmployeesByDepartmentIdStoredProcedure = "SelectAllDepartment";



            var result = await dbContext
                .SelectStoredProcedureListAsync<EmployeesByDepartmentViewModel>(getEmployeesByDepartmentIdStoredProcedure, spParamList);



            Console.WriteLine("Stored Procedure");
            foreach (var department in result)
            {
                Console.WriteLine($"department Id: {department.Id}, # Employee Name: {department.Name}");
            }
            Console.WriteLine("*******************************");
        }



        public async Task sp_Sparda_SelectStoredProcedureListAsync()
        {
            // 

            const string spName = "sp_GetProjectTimelinePaged";

            var sqlParams = new Dictionary<string, object>
            {
                ["ProjectId"] = Guid.Parse("62A33028-4B0C-4460-8729-4BDEF131864C"),
                ["PageNumber"] = 0,
                ["PageSize"] = 100
            };

            // 1️⃣ call the SP – every row already unique to a timeline item
            var result = await dbContext
                .SelectStoredProcedureListAsync<ProjectTimelinePagedRaw>(spName, sqlParams);



            foreach (var response in result)
            {
                Console.WriteLine($"{response.ProjectId} - {response.CreatedDate}");
            }
            Console.WriteLine("Stored Procedure");
        }




        public async Task sp_GetAllProjectRecordsListAsync()
        {
            // 

            var spParamList = new Dictionary<string, object>
            {
                { "Title", "18" }
            };



            // "Stored Procedure"
            const string getEmployeesByDepartmentIdStoredProcedure = "GetAllProjectRecords";

            var result = await dbContext
                .SelectStoredProcedureListAsync<ProjectRecordViewModel>(getEmployeesByDepartmentIdStoredProcedure, spParamList);

            Console.WriteLine("Stored Procedure");
            foreach (var record in result)
            {
                Console.WriteLine($"Project: {record.ProjectId}, Title: {record.Title}, Type: {record.RecordType}, Created: {record.CreatedDate}");
            }
            Console.WriteLine("*******************************");
        }

        
        public async Task SelectSqlQuery_GetAllProjectRecordsListAsync()
        {

            const string selectSqlQueryListAsync = """
                                                   SELECT
                                                       ProjectId,
                                                       TypeId,
                                                       RecordType,
                                                       Title,
                                                       Text,
                                                       CreatedDate,
                                                       TaskStatus,
                                                       TaskPriority,
                                                       ActivityCategory,
                                                       MilestoneName,
                                                       TransactionAmount,
                                                       CurrencySymbol,
                                                       TransactionType,
                                                       FileList
                                                   FROM ProjectRecords
                                                   where  ProjectId = @ProjectId
                                                   """;

            var spParamList = new Dictionary<string, object>
            {
                { "ProjectId", Guid.Parse("9803DDD2-6B59-4C6F-A033-207C71BF086A") }
            };

            var result = await dbContext
                .SelectSqlQueryListAsync<ProjectRecordViewModel>(selectSqlQueryListAsync, spParamList);


            foreach (var record in result)
            {
                Console.WriteLine($"Project: {record.ProjectId}, Title: {record.Title}, Type: {record.RecordType}, Created: {record.CreatedDate}");
            }
            Console.WriteLine("*******************************");
            Console.WriteLine();
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
