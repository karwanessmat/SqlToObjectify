using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SqlToObjectify.Exceptions;
using System.Data;
using System.Data.Common;
using System.Dynamic;

namespace SqlToObjectify
{
    public static class DbContextExtensions
    {

        /// <summary>
        /// Fetch a list of object based on the query
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <param name="sqlQuery"></param>
        /// <param name="parameters"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<List<T>> SelectSqlQueryListAsync<T>(
                                        this DbContext context,
                                        string sqlQuery,
                                        Dictionary<string, object>? parameters = null,
                                        CancellationToken cancellationToken = default) 
        {
            var commandParameters = ConvertToSqlParameters(parameters);
            var connection = context.Database.GetDbConnection();

            await using var command = CreateCommand(connection, sqlQuery,CommandType.Text, commandParameters);
            var result = await ExecuteSqlQueryAsync(command,true, cancellationToken);
            
            return  result.MapToObjectList<T>()!;
        }

        /// <summary>
        /// Fetch an object based on the query
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <param name="sqlQuery"></param>
        /// <param name="parameters"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<T> SelectSqlQueryFirstOrDefaultAsync<T>(
            this DbContext context,
            string sqlQuery,
            Dictionary<string, object>? parameters = null,
            CancellationToken cancellationToken=default) 
        {
            var commandParameters = ConvertToSqlParameters(parameters);
            var connection = context.Database.GetDbConnection();

            await using var command = CreateCommand(connection, sqlQuery, CommandType.Text, commandParameters);
            var result = await ExecuteSqlQueryAsync(command, false,cancellationToken);

            return result.MapToObject<T>()!;
        }



        /// <summary>
        /// Update or Delete an object based the query.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="sqlQuery"></param>
        /// <param name="parameters"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task ExecuteSqlQueryCommandAsync(
            this DbContext context,
            string sqlQuery,
            Dictionary<string, object>? parameters = null,
            CancellationToken cancellationToken = default)
        {
            var commandParameters = ConvertToSqlParameters(parameters);
            var connection = context.Database.GetDbConnection();

            await using var command = CreateCommand(connection, sqlQuery, CommandType.Text, commandParameters);
             await ExecuteSqlCommandAsync(command, cancellationToken);

        }


        /// <summary>
        /// Fetch a list of object based on the stored Procedure
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <param name="sqlQuery"></param>
        /// <param name="parameters"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<List<T>> SelectStoredProcedureListAsync<T>(
            this DbContext context,
            string sqlQuery,
            Dictionary<string, object>? parameters = null,
            CancellationToken cancellationToken = default)
        {
            var commandParameters = ConvertToSqlParameters(parameters);
            var connection = context.Database.GetDbConnection();

            await using var command = CreateCommand(connection, sqlQuery, CommandType.StoredProcedure, commandParameters);
            var result = await ExecuteSqlQueryAsync(command, true, cancellationToken);

            return result.MapToObjectList<T>()!;
        }

        /// <summary>
        /// Fetch an object based on the stored Procedure
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <param name="sqlQuery"></param>
        /// <param name="parameters"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<T> SelectStoredProcedureFirstOrDefaultAsync<T>(
            this DbContext context,
            string sqlQuery,
            Dictionary<string, object>? parameters = null,
            CancellationToken cancellationToken = default)
        {
            var commandParameters = ConvertToSqlParameters(parameters);
            var connection = context.Database.GetDbConnection();

            await using var command = CreateCommand(connection, sqlQuery, CommandType.StoredProcedure, commandParameters);
            var result = await ExecuteSqlQueryAsync(command, false, cancellationToken);

            return result.MapToObject<T>()!;
        }


        /// <summary>
        /// Update or Delete an object based on the stored Procedure
        /// </summary>
        /// <param name="context"></param>
        /// <param name="sqlQuery"></param>
        /// <param name="parameters"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task ExecuteStoredProcedureAsync(
            this DbContext context,
            string sqlQuery,
            Dictionary<string, object>? parameters = null,
            CancellationToken cancellationToken = default)
        {
            var commandParameters = ConvertToSqlParameters(parameters);
            var connection = context.Database.GetDbConnection();

            await using var command = CreateCommand(connection, sqlQuery, CommandType.StoredProcedure, commandParameters);
            await ExecuteSqlCommandAsync(command, cancellationToken);

        }


        private static IEnumerable<SqlParameter> ConvertToSqlParameters(Dictionary<string, object>? parameters)
        {
            return parameters?
                .Select(param => new SqlParameter($"@{param.Key}", param.Value))
                   ?? Enumerable.Empty<SqlParameter>();
        }

        private static DbCommand CreateCommand(DbConnection connection, string sqlQuery, CommandType commandType, IEnumerable<SqlParameter> parameters)
        {
            var command = connection.CreateCommand();
            command.CommandText = sqlQuery;
            //command.CommandType = sqlQuery.Trim().Contains(" ") ? CommandType.Text : CommandType.StoredProcedure;
            command.CommandType = commandType;
            command.Parameters.AddRange(parameters.ToArray());
            return command;
        }

        private static async Task<object> ExecuteSqlQueryAsync(DbCommand command, bool returnList, CancellationToken cancellationToken)
        {
            if (command.Connection?.State != ConnectionState.Open)
                await command.Connection?.OpenAsync(cancellationToken)!;

            try
            {
                var headers = await GetResultHeadersAsync(command, cancellationToken);
                var results = await GetResultsAsync(command, headers, cancellationToken);
                return (returnList ? results : results.FirstOrDefault())!;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Error executing the database command.", ex);
            }
            finally
            {
                if (command.Connection?.State != ConnectionState.Closed)
                    await command.Connection?.CloseAsync()!;
            }
        }

        private static async Task ExecuteSqlCommandAsync(DbCommand command, CancellationToken cancellationToken)
        {
            if (command.Connection?.State != ConnectionState.Open)
                await command.Connection?.OpenAsync(cancellationToken)!;

            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken);

            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Error executing the database command.", ex);
            }
            finally
            {
                if (command.Connection?.State != ConnectionState.Closed)
                    await command.Connection?.CloseAsync()!;
            }
        }



        private static async Task<List<string>> GetResultHeadersAsync(DbCommand command, CancellationToken cancellationToken)
        {
            var headers = new List<string>();
            try
            {
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                for (var i = 0; i < reader.VisibleFieldCount; i++)
                {
                    headers.Add(reader.GetName(i));
                }
            }
            catch (Exception ex)
            {
                throw new SqlExecutionException("Failed to execute the SQL command and retrieve headers.", ex);
            }
            return headers;
        }

        private static async Task<List<ExpandoObject>> GetResultsAsync(DbCommand command, IReadOnlyList<string> headers, CancellationToken cancellationToken)
        {
            var results = new List<ExpandoObject>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
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
