using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Dynamic;

namespace SqlToObjectify
{
    public static class DbContextExtensions
    {
        public static object ReadDataBySqlQuery(this DbContext context, string sqlQuery, Dictionary<string, object> parameters, bool returnList)
        {
            var commandParameters = ConvertToSqlParameters(parameters);
            var connection = context.Database.GetDbConnection();
            using var command = CreateCommand(connection, sqlQuery, commandParameters);

            return ExecuteCommand(command, returnList);
        }

        #region ReadDataBySqlQuery 
        private static IEnumerable<SqlParameter> ConvertToSqlParameters(Dictionary<string, object> parameters)
        {
            return parameters.Select(param => new SqlParameter($"@{param.Key}", param.Value));
        }
        private static DbCommand CreateCommand(DbConnection connection, string sqlQuery, IEnumerable<SqlParameter> parameters)
        {
            var command = connection.CreateCommand();
            command.CommandText = sqlQuery;
            command.Parameters.AddRange(parameters.ToArray());
            return command;
        }

        private static object ExecuteCommand(DbCommand command, bool returnList)
        {
            var connection = command.Connection;

            try
            {
                connection?.Open();

                var headers = GetResultHeaders(command);
                var results = GetResults(command, headers);

                return returnList ? results : results.FirstOrDefault();
            }
            finally
            {
                connection?.Close();
            }
        }

        private static List<string> GetResultHeaders(DbCommand command)
        {
            var headers = new List<string>();
            using var reader = command.ExecuteReader();
            for (var i = 0; i < reader.VisibleFieldCount; i++)
            {
                headers.Add(reader.GetName(i));
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
        #endregion
    }

}
