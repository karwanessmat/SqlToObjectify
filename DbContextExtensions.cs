using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;

namespace SqlToObjectify
{
    public static class DbContextExtensions
    {
        private static readonly ConcurrentDictionary<string, string> ParameterNameCache = new(StringComparer.Ordinal);

        /// <param name="context"></param>
        extension(DbContext context)
        {
            /// <summary>
            /// Fetch a list of object based on the query
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="sqlQuery"></param>
            /// <param name="parameters"></param>
            /// <param name="cancellationToken"></param>
            /// <returns></returns>
            public async Task<List<T>> SelectSqlQueryListAsync<T>(
                string sqlQuery,
                Dictionary<string, object>? parameters = null,
                CancellationToken cancellationToken = default)
            {
                var connection = context.Database.GetDbConnection();
                await using var command = CreateCommand(connection, sqlQuery, CommandType.Text, parameters);
                return await DataReaderObjectMapper.ReadListAsync<T>(command, cancellationToken).ConfigureAwait(false);
            }

            /// <summary>
            /// Fetch an object based on the query
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="sqlQuery"></param>
            /// <param name="parameters"></param>
            /// <param name="cancellationToken"></param>
            /// <returns></returns>
            public async Task<T> SelectSqlQueryFirstOrDefaultAsync<T>(
                string sqlQuery,
                Dictionary<string, object>? parameters = null,
                CancellationToken cancellationToken = default)
            {
                var connection = context.Database.GetDbConnection();
                await using var command = CreateCommand(connection, sqlQuery, CommandType.Text, parameters);
                return await DataReaderObjectMapper.ReadFirstOrDefaultAsync<T>(command, cancellationToken).ConfigureAwait(false);
            }

            /// <summary>
            /// Update or Delete an object based the query.
            /// </summary>
            /// <param name="sqlQuery"></param>
            /// <param name="parameters"></param>
            /// <param name="cancellationToken"></param>
            /// <returns></returns>
            public async Task ExecuteSqlQueryCommandAsync(
                string sqlQuery,
                Dictionary<string, object>? parameters = null,
                CancellationToken cancellationToken = default)
            {
                var connection = context.Database.GetDbConnection();
                await using var command = CreateCommand(connection, sqlQuery, CommandType.Text, parameters);
                await ExecuteNonQueryAsync(command, cancellationToken).ConfigureAwait(false);
            }

            /// <summary>
            /// Fetch a list of object based on the stored Procedure
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="sqlQuery"></param>
            /// <param name="parameters"></param>
            /// <param name="cancellationToken"></param>
            /// <returns></returns>
            public async Task<List<T>> SelectStoredProcedureListAsync<T>(
                string sqlQuery,
                Dictionary<string, object>? parameters = null,
                CancellationToken cancellationToken = default)
            {
                var connection = context.Database.GetDbConnection();
                await using var command = CreateCommand(connection, sqlQuery, CommandType.StoredProcedure, parameters);
                return await DataReaderObjectMapper.ReadListAsync<T>(command, cancellationToken).ConfigureAwait(false);
            }

            /// <summary>
            /// Fetch an object based on the stored Procedure
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="sqlQuery"></param>
            /// <param name="parameters"></param>
            /// <param name="cancellationToken"></param>
            /// <returns></returns>
            public async Task<T> SelectStoredProcedureFirstOrDefaultAsync<T>(
                string sqlQuery,
                Dictionary<string, object>? parameters = null,
                CancellationToken cancellationToken = default)
            {
                var connection = context.Database.GetDbConnection();
                await using var command = CreateCommand(connection, sqlQuery, CommandType.StoredProcedure, parameters);
                return await DataReaderObjectMapper.ReadFirstOrDefaultAsync<T>(command, cancellationToken).ConfigureAwait(false);
            }

            /// <summary>
            /// Update or Delete an object based on the stored Procedure.
            /// </summary>
            /// <param name="sqlQuery"></param>
            /// <param name="parameters"></param>
            /// <param name="cancellationToken"></param>
            /// <returns></returns>
            public async Task ExecuteStoredProcedureAsync(
                string sqlQuery,
                Dictionary<string, object>? parameters = null,
                CancellationToken cancellationToken = default)
            {
                var connection = context.Database.GetDbConnection();
                await using var command = CreateCommand(connection, sqlQuery, CommandType.StoredProcedure, parameters);
                await ExecuteNonQueryAsync(command, cancellationToken).ConfigureAwait(false);
            }
        }

        private static DbCommand CreateCommand(
            DbConnection connection,
            string sqlQuery,
            CommandType commandType,
            IReadOnlyDictionary<string, object>? parameters)
        {
            var command = connection.CreateCommand();
            command.CommandText = sqlQuery;
            command.CommandType = commandType;

            if (parameters is null || parameters.Count == 0)
                return command;

            foreach (var (key, value) in parameters)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = key.StartsWith("@", StringComparison.Ordinal)
                    ? key
                    : ParameterNameCache.GetOrAdd(key, static k => "@" + k);
                parameter.Value = value ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }

            return command;
        }

        private static async Task ExecuteNonQueryAsync(DbCommand command, CancellationToken cancellationToken)
        {
            var connection = command.Connection ?? throw new InvalidOperationException("DbCommand.Connection is null.");
            var shouldClose = connection.State != ConnectionState.Open;

            if (shouldClose)
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (shouldClose && connection.State != ConnectionState.Closed)
                    await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }
}
