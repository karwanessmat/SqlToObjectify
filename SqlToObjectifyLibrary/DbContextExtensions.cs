using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace SqlToObjectifyLibrary
{
    public static class DbContextExtensions
    {
        // ---------------------------------------------------------------------------
        // Internal compiled-command cache
        //
        // The first call to SelectSqlQueryListAsync (or the stored-procedure variant)
        // builds a DbCommand, executes it, and resolves the RowFactory — identical to
        // the first call of CompiledSqlQuery<T>.  The command is then stored in this
        // cache keyed by (connection identity, sql text, CommandType).
        //
        // Every subsequent call for the same query on the same connection:
        //   • reuses the already-created DbCommand (no CreateCommand / CreateParameter)
        //   • skips GetOrAdd in RowFactoryCache<T> (factory already stored)
        //   • pre-sizes List<T> from the previous call's count (_lastCount)
        //   • updates parameter values by ordinal index (no string lookup)
        //
        // This gives SelectSqlQueryListAsync the same hot-path cost as
        // CompiledSqlQuery<T>.ToListAsync while keeping the simple dictionary API.
        // ---------------------------------------------------------------------------
        private sealed class InternalCompiledEntry<T>(
            DbCommand command,
            string[]? paramPrefixed,
            string[]? paramUnprefixed)
        {
            public readonly DbCommand Command = command;
            // "@name" forms — matches callers who include the prefix.
            public readonly string[]? ParamPrefixed = paramPrefixed;
            // "name" forms — matches callers who omit the prefix. Zero-allocation fallback.
            public readonly string[]? ParamUnprefixed = paramUnprefixed;
            public DataReaderObjectMapper.RowFactoryCache<T>.RowFactory? Factory;
            public int LastCount;
        }

        // Key: (connection, sql text, command-type, DTO type).
        // Including typeof(T) ensures that the same SQL with different DTOs gets separate
        // cached commands (each DTO type needs its own RowFactory).
        private static readonly ConcurrentDictionary<(DbConnection, string, CommandType, Type), object> _entryCache = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static InternalCompiledEntry<T> GetOrCreateEntry<T>(
            DbConnection connection,
            string sqlQuery,
            CommandType commandType,
            IReadOnlyDictionary<string, object>? parameters)
        {
            var key = (connection, sqlQuery, commandType, typeof(T));

            if (_entryCache.TryGetValue(key, out var existing))
                return (InternalCompiledEntry<T>)existing;

            return GetOrCreateEntrySlow<T>(key, connection, sqlQuery, commandType, parameters);
        }

        private static InternalCompiledEntry<T> GetOrCreateEntrySlow<T>(
            (DbConnection, string, CommandType, Type) key,
            DbConnection connection,
            string sqlQuery,
            CommandType commandType,
            IReadOnlyDictionary<string, object>? parameters)
        {
            // Build the command once with parameters pre-attached.
            var command = CreateCommand(connection, sqlQuery, commandType, parameters);

            // Store both "@name" and "name" forms for zero-allocation lookups on the hot path.
            string[]? prefixed = null;
            string[]? unprefixed = null;
            if (parameters is { Count: > 0 })
            {
                var count = command.Parameters.Count;
                prefixed = new string[count];
                unprefixed = new string[count];
                for (var i = 0; i < count; i++)
                {
                    var pname = command.Parameters[i].ParameterName;
                    prefixed[i] = pname;
                    unprefixed[i] = pname.StartsWith("@", StringComparison.Ordinal) ? pname.Substring(1) : pname;
                }
            }

            var entry = new InternalCompiledEntry<T>(command, prefixed, unprefixed);

            // GetOrAdd is safe: if another thread races us, we discard our command and use theirs.
            var winner = (InternalCompiledEntry<T>)_entryCache.GetOrAdd(key, entry);
            if (!ReferenceEquals(winner, entry))
                command.Dispose(); // we lost the race; discard our command
            return winner;
        }

        /// <param name="context"></param>
        extension(DbContext context)
        {
            /// <summary>
            /// Fetch a list of object based on the query.
            /// After the first call, the DbCommand is reused and the row-mapping delegate is
            /// cached, giving the same hot-path performance as a <see cref="CompiledSqlQuery{T}"/>.
            /// </summary>
            public async Task<List<T>> SelectSqlQueryListAsync<T>(
                string sqlQuery,
                Dictionary<string, object>? parameters = null,
                CancellationToken cancellationToken = default)
            {
                var connection = context.Database.GetDbConnection();
                var entry = GetOrCreateEntry<T>(connection, sqlQuery, CommandType.Text, parameters);

                if (parameters is { Count: > 0 })
                    UpdateCommandParametersFast(entry, parameters);

                List<T> result;
                if (entry.Factory.HasValue)
                {
                    result = await DataReaderObjectMapper.ReadListAsync(
                        entry.Command, entry.Factory.Value, entry.LastCount, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    var (r, factory) = await DataReaderObjectMapper.ReadListWithFactoryAsync<T>(
                        entry.Command, entry.LastCount, cancellationToken)
                        .ConfigureAwait(false);
                    entry.Factory = factory;
                    result = r;
                }

                entry.LastCount = result.Count;
                return result;
            }

            /// <summary>
            /// Fetch an object based on the query.
            /// After the first call, the DbCommand and row-mapping delegate are cached.
            /// </summary>
            public async Task<T> SelectSqlQueryFirstOrDefaultAsync<T>(
                string sqlQuery,
                Dictionary<string, object>? parameters = null,
                CancellationToken cancellationToken = default)
            {
                var connection = context.Database.GetDbConnection();
                var entry = GetOrCreateEntry<T>(connection, sqlQuery, CommandType.Text, parameters);

                if (parameters is { Count: > 0 })
                    UpdateCommandParametersFast(entry, parameters);

                if (entry.Factory.HasValue)
                {
                    return await DataReaderObjectMapper.ReadFirstOrDefaultAsync(
                        entry.Command, entry.Factory.Value, cancellationToken)
                        .ConfigureAwait(false);
                }

                var (result, factory) = await DataReaderObjectMapper.ReadFirstOrDefaultWithFactoryAsync<T>(
                    entry.Command, cancellationToken)
                    .ConfigureAwait(false);
                entry.Factory = factory;
                return result;
            }

            /// <summary>
            /// Update or Delete an object based the query.
            /// </summary>
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
            /// Fetch a list of object based on the stored Procedure.
            /// After the first call, the DbCommand is reused and the row-mapping delegate is
            /// cached, giving the same hot-path performance as a <see cref="CompiledSqlQuery{T}"/>.
            /// </summary>
            public async Task<List<T>> SelectStoredProcedureListAsync<T>(
                string sqlQuery,
                Dictionary<string, object>? parameters = null,
                CancellationToken cancellationToken = default)
            {
                var connection = context.Database.GetDbConnection();
                var entry = GetOrCreateEntry<T>(connection, sqlQuery, CommandType.StoredProcedure, parameters);

                if (parameters is { Count: > 0 })
                    UpdateCommandParametersFast(entry, parameters);

                List<T> result;
                if (entry.Factory.HasValue)
                {
                    result = await DataReaderObjectMapper.ReadListAsync(
                        entry.Command, entry.Factory.Value, entry.LastCount, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    var (r, factory) = await DataReaderObjectMapper.ReadListWithFactoryAsync<T>(
                        entry.Command, entry.LastCount, cancellationToken)
                        .ConfigureAwait(false);
                    entry.Factory = factory;
                    result = r;
                }

                entry.LastCount = result.Count;
                return result;
            }

            /// <summary>
            /// Fetch an object based on the stored Procedure.
            /// After the first call, the DbCommand and row-mapping delegate are cached.
            /// </summary>
            public async Task<T> SelectStoredProcedureFirstOrDefaultAsync<T>(
                string sqlQuery,
                Dictionary<string, object>? parameters = null,
                CancellationToken cancellationToken = default)
            {
                var connection = context.Database.GetDbConnection();
                var entry = GetOrCreateEntry<T>(connection, sqlQuery, CommandType.StoredProcedure, parameters);

                if (parameters is { Count: > 0 })
                    UpdateCommandParametersFast(entry, parameters);

                if (entry.Factory.HasValue)
                {
                    return await DataReaderObjectMapper.ReadFirstOrDefaultAsync(
                        entry.Command, entry.Factory.Value, cancellationToken)
                        .ConfigureAwait(false);
                }

                var (result, factory) = await DataReaderObjectMapper.ReadFirstOrDefaultWithFactoryAsync<T>(
                    entry.Command, cancellationToken)
                    .ConfigureAwait(false);
                entry.Factory = factory;
                return result;
            }

            /// <summary>
            /// Update or Delete an object based on the stored Procedure.
            /// </summary>
            public async Task ExecuteStoredProcedureAsync(
                string sqlQuery,
                Dictionary<string, object>? parameters = null,
                CancellationToken cancellationToken = default)
            {
                var connection = context.Database.GetDbConnection();
                await using var command = CreateCommand(connection, sqlQuery, CommandType.StoredProcedure, parameters);
                await ExecuteNonQueryAsync(command, cancellationToken).ConfigureAwait(false);
            }

            /// <summary>
            /// Stream results row-by-row from a SQL query without buffering into a List.
            /// Ideal for large result sets where you want to process rows as they arrive.
            /// </summary>
            public IAsyncEnumerable<T> SelectSqlQueryStreamAsync<T>(
                string sqlQuery,
                Dictionary<string, object>? parameters = null,
                CancellationToken cancellationToken = default)
            {
                var connection = context.Database.GetDbConnection();
                var command = CreateCommand(connection, sqlQuery, CommandType.Text, parameters);
                return DataReaderObjectMapper.StreamAsync<T>(command, cancellationToken);
            }

            /// <summary>
            /// Stream results row-by-row from a stored procedure without buffering into a List.
            /// Ideal for large result sets where you want to process rows as they arrive.
            /// </summary>
            public IAsyncEnumerable<T> SelectStoredProcedureStreamAsync<T>(
                string storedProcedureName,
                Dictionary<string, object>? parameters = null,
                CancellationToken cancellationToken = default)
            {
                var connection = context.Database.GetDbConnection();
                var command = CreateCommand(connection, storedProcedureName, CommandType.StoredProcedure, parameters);
                return DataReaderObjectMapper.StreamAsync<T>(command, cancellationToken);
            }

            public CompiledSqlQuery<T> CompileSqlQuery<T>(string sqlQuery, params string[] parameterNames)
            {
                var connection = context.Database.GetDbConnection();
                return new CompiledSqlQuery<T>(connection, sqlQuery, CommandType.Text, parameterNames);
            }

            public CompiledSqlQuery<T> CompileStoredProcedure<T>(string storedProcedureName, params string[] parameterNames)
            {
                var connection = context.Database.GetDbConnection();
                return new CompiledSqlQuery<T>(connection, storedProcedureName, CommandType.StoredProcedure, parameterNames);
            }
        }

        // ---------------------------------------------------------------------------
        // Helpers — shared between first-call (CreateCommand) and hot-path (UpdateCommandParameters)
        // ---------------------------------------------------------------------------

        // Used on first call: creates a brand-new command with all parameters attached.
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
                parameter.ParameterName = key.StartsWith("@", StringComparison.Ordinal) ? key : "@" + key;

                if (value is not null)
                    ConfigureParameter(parameter, value);

                parameter.Value = value ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }

            return command;
        }

        // Hot-path: updates parameter values by ordinal index using the pre-stored name array.
        // Avoids string normalization and string-keyed DbParameterCollection lookups entirely.
        // Hot-path: updates parameter values by ordinal index using pre-stored name arrays.
        // Zero string allocation — tries "@name" then "name" with pre-computed strings.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpdateCommandParametersFast<T>(
            InternalCompiledEntry<T> entry,
            IReadOnlyDictionary<string, object> parameters)
        {
            var dbParams = entry.Command.Parameters;
            var prefixed = entry.ParamPrefixed;
            var unprefixed = entry.ParamUnprefixed;
            if (prefixed is null) return;

            for (var i = 0; i < prefixed.Length; i++)
            {
                // Try "@name" first (common for explicit callers), then "name" (common for
                // callers who let the library add the prefix).  Both strings are pre-computed
                // so this path is allocation-free.
                if (!parameters.TryGetValue(prefixed[i], out var value) &&
                    !parameters.TryGetValue(unprefixed![i], out value))
                    continue;

                var param = dbParams[i];

                // For string NVarChar parameters: keep the existing SqlDbType + Size,
                // just update Value.  This matches what CompiledSqlQuery<T>.SetParameter(string) does.
                if (param is SqlParameter sp && sp.SqlDbType == SqlDbType.NVarChar && value is string sv)
                {
                    var desiredSize = sv.Length <= 4000 ? 4000 : -1;
                    if (sp.Size != desiredSize)
                        sp.Size = desiredSize;
                    sp.Value = sv;
                }
                else
                {
                    param.Value = value ?? DBNull.Value;
                }
            }
        }

        private static void ConfigureParameter(DbParameter parameter, object value)
        {
            if (parameter is SqlParameter sqlParameter)
            {
                ConfigureSqlParameter(sqlParameter, value);
                return;
            }

            // Minimal provider-agnostic hints (still keeps existing behavior when not supported).
            switch (value)
            {
                case int:
                    parameter.DbType = DbType.Int32;
                    break;
                case long:
                    parameter.DbType = DbType.Int64;
                    break;
                case short:
                    parameter.DbType = DbType.Int16;
                    break;
                case byte:
                    parameter.DbType = DbType.Byte;
                    break;
                case bool:
                    parameter.DbType = DbType.Boolean;
                    break;
                case Guid:
                    parameter.DbType = DbType.Guid;
                    break;
                case DateTime:
                    parameter.DbType = DbType.DateTime2;
                    break;
                case DateTimeOffset:
                    parameter.DbType = DbType.DateTimeOffset;
                    break;
                case decimal:
                    parameter.DbType = DbType.Decimal;
                    break;
                case double:
                    parameter.DbType = DbType.Double;
                    break;
                case float:
                    parameter.DbType = DbType.Single;
                    break;
                case string:
                    parameter.DbType = DbType.String;
                    break;
                case byte[]:
                    parameter.DbType = DbType.Binary;
                    break;
            }
        }

        private static void ConfigureSqlParameter(SqlParameter parameter, object value)
        {
            // Fast + stable parameter metadata for SQL Server (helps plan caching / index usage).
            switch (value)
            {
                case int:
                    parameter.SqlDbType = SqlDbType.Int;
                    break;
                case long:
                    parameter.SqlDbType = SqlDbType.BigInt;
                    break;
                case short:
                    parameter.SqlDbType = SqlDbType.SmallInt;
                    break;
                case byte:
                    parameter.SqlDbType = SqlDbType.TinyInt;
                    break;
                case bool:
                    parameter.SqlDbType = SqlDbType.Bit;
                    break;
                case Guid:
                    parameter.SqlDbType = SqlDbType.UniqueIdentifier;
                    break;
                case DateTime:
                    parameter.SqlDbType = SqlDbType.DateTime2;
                    break;
                case DateTimeOffset:
                    parameter.SqlDbType = SqlDbType.DateTimeOffset;
                    break;
                case decimal:
                    parameter.SqlDbType = SqlDbType.Decimal;
                    break;
                case double:
                    parameter.SqlDbType = SqlDbType.Float;
                    break;
                case float:
                    parameter.SqlDbType = SqlDbType.Real;
                    break;
                case string s:
                    parameter.SqlDbType = SqlDbType.NVarChar;
                    var desiredSize = s.Length <= 4000 ? 4000 : -1;
                    if (parameter.Size != desiredSize)
                        parameter.Size = desiredSize;
                    break;
                case byte[] bytes:
                    parameter.SqlDbType = SqlDbType.VarBinary;
                    var desiredBinarySize = bytes.Length <= 8000 ? bytes.Length : -1;
                    if (parameter.Size != desiredBinarySize)
                        parameter.Size = desiredBinarySize;
                    break;
            }
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
