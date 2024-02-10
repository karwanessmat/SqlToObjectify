


# EF Core SQL to Object Utility

Hello 🖐️ 

Welcome to the EF Core SQL to Object Mapping Utility, a powerful tool designed to simplify the execution of raw SQL queries and the mapping of their results to strongly-typed .NET objects within an Entity Framework Core (EF Core) context. This utility enhances developer productivity by bridging the gap between the flexibility of SQL and the type safety of .NET, making data access more efficient and secure..


## Features 🌟

- **Asynchronous Query Execution**: Perform non-blocking SQL query and stored procedure executions directly from your `DbContext`.
- **Dynamic Parameterization**: Securely pass parameters to your queries and stored procedures to prevent SQL injection attacks.
-   **Automatic Result Mapping**: Easily map dynamic query results to strongly-typed objects or lists, leveraging the full capabilities of C# and .NET.



## Getting Started

Incorporate the `DbContextExtensions` and `ObjectMapper` into your project to start enhancing your EF Core operations. These components are crucial for extending your `DbContext` with powerful functionalities:

1.  **`DbContextExtensions`**: Provides methods for executing SQL commands, including queries and stored procedures, with support for asynchronous operations.
2.  **`ObjectMapper`**: Facilitates the conversion of dynamic results into strongly-typed entities using reflection.

### Quick Notes

-   **Parameter Matching**: Ensure dictionary keys for parameters match SQL query placeholders (without the `@` prefix).
-   **Testing Queries**: Always test SQL queries in a controlled environment before integration.
-   **SQL Syntax Awareness**: Pay attention to SQL syntax, including spaces and special characters, to avoid unexpected behavior.

**Parameters**:
-   `sqlQuery`: The SQL query string.
-   `parameters`: a dictionary of parameters to be passed to the SQL query.




### Example Implementations

Here's how you can leverage our utility in your projects:


#### 1. `SelectSqlQueryListAsync<T>`: Executes a SQL query asynchronously and maps the results to a list of strongly-typed objects. It's useful for queries expected to return multiple rows.

```csharp
var result= await context
				.SelectSqlQueryListAsync<ViewModel>(query, paramList);


```

#### 2.**`SelectSqlQueryFirstOrDefaultAsync<T>`**: Asynchronously executes a SQL query and maps the first result to a strongly-typed object. Ideal for queries where only a single result is expected.

```csharp
var result= await context
				.SelectSqlQueryFirstOrDefaultAsync<ViewModel>(query, paramList);


```

#### 3.**`ExecuteSqlQueryCommandAsync`**: Executes a SQL command (e.g., update, delete) asynchronously without returning any results. Useful for data manipulation operations.

```csharp
await context.ExecuteSqlQueryCommandAsync(updateEmployeeFromExecuteQuery, updateParamList);


```


#### 4.**`SelectStoredProcedureListAsync<T>`**: Executes a stored procedure asynchronously, returning the results as a list of strongly-typed objects. Suitable for stored procedures expected to return multiple rows.

```csharp
var result= await context
				.SelectStoredProcedureListAsync<ViewModel>(sp_name, paramList);

```

#### 5.**`SelectStoredProcedureFirstOrDefaultAsync<T>`**: Executes a stored procedure and maps the first result to a strongly-typed object. Used when a stored procedure is expected to return a single row.

```csharp
var result= await context
					.SelectStoredProcedureFirstOrDefaultAsync<ViewModel>
					(sp_name, paramList);


```

#### 6.**`ExecuteStoredProcedureAsync`**: Executes a stored procedure without returning any results, typically used for operations like update or delete through stored procedures.

```csharp
await context.ExecuteStoredProcedureAsync(sp_name, paramList);
```



## Contributions and Feedback

Your feedback and contributions are welcome to help us improve and expand the utility. Please feel free to reach out through GitHub or LinkedIn for discussions, suggestions, or contributions.


## 📄 License
[MIT](https://choosealicense.com/licenses/mit/)
    

## 🔗 Connect with me:

[<img align="left" alt="GitHub" width="22px" src="https://raw.githubusercontent.com/iconic/open-iconic/master/svg/globe.svg" />][github] /karwan
[<img align="left" alt="LinkedIn" width="22px" src="https://cdn.jsdelivr.net/npm/simple-icons@v3/icons/linkedin.svg" />][linkedin] /karwan

[github]: https://github.com/karwanessmat
[linkedin]: https://www.linkedin.com/in/karwan-othman

 

## Feedback 📢
If you have any feedback, please reach out to us at karwan.essmat@gmail.com 
  
