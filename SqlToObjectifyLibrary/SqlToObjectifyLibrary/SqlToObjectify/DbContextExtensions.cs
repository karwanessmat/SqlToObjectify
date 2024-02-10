using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;
using System.Dynamic;
using SqlToObjectify.Exceptions;

namespace SqlToObjectify
{
    public static class DbContextExtensions
    {
        public static object ExecuteSqlQuery(
                                    this DbContext context,
                                    string sqlQuery,
                                    Dictionary<string, object>? parameters = null,
                                    bool returnList = true)
        {
            var commandParameters = ConvertToSqlParameters(parameters);
            var connection = context.Database.GetDbConnection();

            using var command = CreateCommand(connection, sqlQuery, commandParameters);
            return ExecuteCommand(command, returnList);
        }

        private static IEnumerable<SqlParameter> ConvertToSqlParameters(Dictionary<string, object>? parameters)
        {
            return parameters?
                .Select(param => new SqlParameter($"@{param.Key}", param.Value))
                   ?? Enumerable.Empty<SqlParameter>();
        }

        private static DbCommand CreateCommand(DbConnection connection, string sqlQuery, IEnumerable<SqlParameter> parameters)
        {
            var command = connection.CreateCommand();
            command.CommandText = sqlQuery;
            command.CommandType = sqlQuery.Trim().Contains(" ")
                                            ? CommandType.Text
                                            : CommandType.StoredProcedure;

            command.Parameters.AddRange(parameters.ToArray());

            return command;
        }

        private static object ExecuteCommand(DbCommand command, bool returnList)
        {
            // Using the null- operator to simplify the connection checks.
            if (command.Connection?.State != ConnectionState.Open)
                command.Connection?.Open();

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
                if (command.Connection?.State != ConnectionState.Closed)
                    command.Connection?.Close();
            }
        }


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

        private static ExpandoObject? ReadRow(IDataRecord reader, IReadOnlyList<string> headers)
        {
            var row = new ExpandoObject() as IDictionary<string, object?>;

            for (var i = 0; i < reader.FieldCount; i++)
            {
                var columnName = headers[i];
                var value = reader.GetValue(i);
                row[columnName] = value is DBNull ? null : value;
            }

            return row as ExpandoObject;
        }


    }

}

