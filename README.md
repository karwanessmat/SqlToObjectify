

# EF Core SQL to Object Utility

Hello, developers! 🖐️ 

After running SQL in EF Core and getting bad LINQ returns for complex queries, I wanted a more straightforward method. Methods of handling situations and saving time: Provides case prevention. This solution lets Entity Framework Core developers convert SQL queries to strongly typed lists or objects. I hope it helps.



## Table of Contents 📜
- [Features](#features)
- [DbContextExtensions](#DbContextExtensions)
- [ObjectMapper](#ObjectMapper)
- [UsageExample](#UsageExample)


## Features 🌟

-   Execute raw SQL queries directly from your `DbContext`.
-   Map the results to strongly-typed objects or lists.
-   Dynamic object mapping using the `ExpandoObject`.
-   Supports parameterized queries for added security against SQL injection.

## DbContextExtensions 🛠️

This static class provides the main functionality:
### `ReadDataBySqlQuery`

**Purpose**: Main method to execute a SQL query.

**Parameters**:
-   `sqlQuery`: The SQL query string.
-   `parameters`: Dictionary of parameters to be passed to the SQL query.
-   `returnList`: Boolean flag to determine if the result should be a list or a single object, by default is true.

**Returns**: Raw SQL query result.

```csharp
public static object ExecuteSqlQuery(this DbContext context, string sqlQuery, Dictionary<string, object>? parameters = null, bool returnList = true)

{
    var commandParameters = ConvertToSqlParameters(parameters);
    var connection = context.Database.GetDbConnection();
    using var command = CreateCommand(connection, sqlQuery, commandParameters);

    return ExecuteCommand(command, returnList);
}
```
### Helper Methods

#### `ConvertToSqlParameters`

Converts a dictionary of parameters into SQL parameters.

```csharp
private static IEnumerable<SqlParameter> ConvertToSqlParameters(Dictionary<string, object>? parameters)
        {
            return parameters?.Select(param => new SqlParameter($"@{param.Key}", param.Value)) ?? Enumerable.Empty<SqlParameter>();
        }
```

#### `CreateCommand`
Creates a database command with the provided SQL query and parameters.

```csharp
       private static DbCommand CreateCommand(DbConnection connection,
        string sqlQuery, IEnumerable<SqlParameter> parameters)
        {
            var command = connection.CreateCommand();
            command.CommandText = sqlQuery;
            command.CommandType = sqlQuery.Trim().Contains(" ") 
            ? CommandType.Text 
            : CommandType.StoredProcedure;
            
            command.Parameters.AddRange(parameters.ToArray());
            return command;
        }
```

#### `ExecuteCommand`

Executes the command and retrieves the results.
```csharp
        private static object ExecuteCommand(DbCommand command, bool returnList)
        {
            // Using the null- operator to simplify the connection checks.
            if (command.Connection?.State != ConnectionState.Open)
                command?.Connection?.Open();

            try
            {
                var headers = GetResultHeaders(command);
                var results = GetResults(command, headers);

                return (returnList 
                        ? results 
                        : results.FirstOrDefault())!;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Error executing the database command.", ex);
            }
            finally
            {
                if (command?.Connection?.State != ConnectionState.Closed)
                    command?.Connection?.Close();
            }
        }
```

#### `GetResultHeaders`

Retrieves the column headers from the result.
```csharp
        private static List<string> GetResultHeaders(DbCommand command)
        {
            var headers = new List<string>();
            try
            {
                using var reader = command.ExecuteReader();
                for (var i = 0; i < reader.VisibleFieldCount; i++)
                {
                    headers.Add(reader.GetName(i));
                }
            }
            catch (Exception ex)
            {
                var exceptionType = ex.GetType().Name;
                var exceptionMessage = ex.Message;
                throw new SqlExecutionException("Failed to execute the SQL command and retrieve headers.", ex);

            }
            return headers;
        }
```
#### `GetResults`

Reads the data from the database and maps it to dynamic objects.
```csharp
private static List<ExpandoObject> GetResults(DbCommand command, IReadOnlyList<string> headers)
{
    var results = new List<ExpandoObject>();
    using var reader = command.ExecuteReader();
    while (reader.Read())
    {
        var result = ReadRow(reader, headers);
        if (result != null)
        {
            results.Add(result);
        }
    }
    return results;
}
```
#### `ReadRow`

Maps a single row from the result to a dynamic object.
```csharp
private static ExpandoObject? ReadRow(DbDataReader reader, IReadOnlyList<string> headers)
{
    var row = new ExpandoObject() as IDictionary<string, object?>;

    for (var i = 0; i < reader.VisibleFieldCount; i++)
    {
        var columnName = headers[i];
        var value = reader.GetValue(i);

        if (value is DBNull)
        {
            return null;
        }
        row[columnName] = value;
    }
    return row as ExpandoObject;
}
```

## ObjectMapper 📌
This static class provides methods to map dynamic objects to strongly-typed objects:

### `MapToObject`

**Purpose**: Map a single dynamic object to a strongly-typed object.

**Parameters**:

-   `result`: The dynamic object.

**Returns**: Strongly-typed object.
```csharp
public static T? MapToObject<T>(this object? result) where T : class, new()
{
    if (result is not ExpandoObject obj)
    {
        return default(T);
    }
    
    var model = Activator.CreateInstance<T>();
    var type = typeof(T);
    SetProperties(obj, type, model);

    return model;
}
```
### `MapToObjectList`

**Purpose**: Map a list of dynamic objects to a list of strongly-typed objects.

**Parameters**:

-   `result`: The list of dynamic objects.

**Returns**: List of strongly-typed objects.
```csharp
public static List<T>? MapToObjectList<T>(this object? result)
{
    if (result is not List<ExpandoObject> listObjList)
    {
        return default;
    }
    var modelList = new List<T>();
    var type = typeof(T);

    foreach (var listObj in listObjList)
    {
        var listItem = Activator.CreateInstance<T>();
        // listItem = new List<T>(); // 
        SetProperties(listObj, type, listItem);
        modelList.Add(listItem);
    }

    return modelList;
}
```

### Helper Method

#### `SetProperties`

Sets properties of the strongly-typed object based on the dynamic object.
```csharp
private static void SetProperties<T>(ExpandoObject obj, IReflect type, T model)
{
    foreach (var (key, value) in obj)
    {
        var property = type.GetProperty(key,
            BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (property == null) continue;

        property.SetValue(model, Convert.ChangeType(value, property.PropertyType));
    }
}
```
## Sample Models 📚
- `Department`: Represents a department.
- `Employee`: Represents an employee.
- `DepartmentEmployeeCountViewModel`: Represents a view model.
- `NumberOfEmployeeInDepartmentViewModel`: Represents another view model.

## UsageExample 📖

The provided example demonstrates how to use the utility to execute a SQL query that retrieves the number of employees in departments and maps the results to a list of `DepartmentEmployeeCountViewModel`.

### Setting and Using Parameters for the SQL Query

When constructing the dictionary of parameters to pass to the SQL query, it's crucial to ensure that the key names in the dictionary match the parameter names in the SQL query.

For instance, in the code:
```csharp
var paramList1= new Dictionary<string, object>
{
    { "numberOfEmployees", 4 }
};
```
The key `"numberOfEmployees"` is used to represent a parameter. In the SQL query, this corresponds to the `@numberOfEmployees` parameter.

This means that when you see `@numberOfEmployees` in the SQL query, it will be replaced with the value `4` from the dictionary during execution.

**Note**: Always ensure that the dictionary key names match the parameter names in the SQL query (without the `@` symbol). Any mismatch can lead to errors or unexpected results.


⚠️ **Caution**: Before setting any SQL query using this utility, always test your query in a safe environment to ensure it works as expected. Additionally, be mindful of spaces between words and other SQL syntax to prevent exceptions or unintended behavior.

### 1. Retrieving a List of Objects with Parameters
This example demonstrates how to use the utility to execute a SQL query that retrieves the number of employees in departments based on a parameter and maps the results to a list of `DepartmentEmployeeCountViewModel`
```csharp
using var context = new SqlObjectDbContext();
const string sqlQuery1 =
    @"SELECT d.Name AS DepartmentName, COUNT(e.Id) AS NumberOfEmployees 
    FROM Departments d inner JOIN Employees e ON d.Id = e.DepartmentId 
    GROUP BY d.Name 
    having  COUNT(e.Id)>=@numberOfEmployees 
    ORDER BY  d.Name";

var paramList1= new Dictionary<string, object>
{
    { "numberOfEmployees", 4 } 
};

var departmentsWithEmployeesCount = context
    .ExecuteSqlQuery(sqlQuery1, paramList1)
    .MapToObjectList<DepartmentEmployeeCountViewModel>();

if (departmentsWithEmployeesCount != null)
    foreach (var department in departmentsWithEmployeesCount)
    {
        Console.WriteLine($"Name: {department.DepartmentName}, # Employees: {department.NumberOfEmployees}");
    }
```

### 2. Retrieving a Single Object with Parameters
This example demonstrates how to retrieve the number of employees in a specific department based on a parameter and map the result to a `NumberOfEmployeeInDepartmentViewModel`.
Another example demonstrates how to retrieve the number of employees in a specific department and map the result to a `NumberOfEmployeeInDepartmentViewModel`.

```csharp
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
```
### 3. Retrieving a List of Objects without Parameters

This example demonstrates how to use the utility to execute a SQL query that retrieves the number of employees in all departments without any parameters and maps the results to a list of `DepartmentEmployeeCountViewModel`.
```csharp
// without parameters
const string sqlQuery3=
    "SELECT d.Name AS DepartmentName, COUNT(e.Id) AS NumberOfEmployees " +
    "FROM Departments d inner JOIN Employees e ON d.Id = e.DepartmentId " +
    "GROUP BY d.Name " +
    "ORDER BY  d.Name";

var allDepartmentsWithEmployeesCount = context
    .ExecuteSqlQuery(sqlQuery3)
    .MapToObjectList<DepartmentEmployeeCountViewModel>();

if (allDepartmentsWithEmployeesCount != null)
    foreach (var department in allDepartmentsWithEmployeesCount)
    {
        Console.WriteLine($"Name: {department.DepartmentName}, # Employees: {department.NumberOfEmployees}");
    }
```

### 4. Executing Stored Procedures with Parameters
The utility now supports executing stored procedures with parameters. This enhancement allows for more dynamic interactions with your database routines.

Here's an example demonstrating how to use the utility to execute a stored procedure named `GetEmployeesByDepartmentId`:
```csharp
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
```



## 📄 License
[MIT](https://choosealicense.com/licenses/mit/)
    

## 🔗 Connect with me:

[<img align="left" alt="GitHub" width="22px" src="https://raw.githubusercontent.com/iconic/open-iconic/master/svg/globe.svg" />][github] /karwan
[<img align="left" alt="LinkedIn" width="22px" src="https://cdn.jsdelivr.net/npm/simple-icons@v3/icons/linkedin.svg" />][linkedin] /karwan

[github]: https://github.com/karwanessmat
[linkedin]: https://www.linkedin.com/in/karwan-othman

 

## Feedback 📢
If you have any feedback, please reach out to us at karwan.essmat@gmail.com 
  
