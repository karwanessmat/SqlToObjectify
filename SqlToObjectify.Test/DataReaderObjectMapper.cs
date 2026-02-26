using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Data.SqlClient;

namespace SqlToObjectify.Test;

internal static class DataReaderObjectMapper
{
    // Default initial capacity for result lists — avoids the first few List<T> doublings
    // for typical query sizes without over-allocating on small result sets.
    private const int DefaultListCapacity = 256;

    public static async Task<List<T>> ReadListAsync<T>(DbCommand command, CancellationToken cancellationToken)
    {
        var connection = command.Connection ?? throw new InvalidOperationException("DbCommand.Connection is null.");
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // SequentialAccess tells SqlClient it can discard already-read column buffers row-by-row,
            // reducing internal buffer allocations for string/binary columns.
            // Safe here because the compiled lambda always reads columns in ascending ordinal order.
            await using var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SingleResult | CommandBehavior.SequentialAccess,
                cancellationToken).ConfigureAwait(false);

            var factory = RowFactoryCache<T>.GetOrAdd(reader, command.CommandText);
            return await ReadAllAsync(reader, factory, 0, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (shouldClose && connection.State != ConnectionState.Closed)
                await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    // Called by CompiledSqlQuery<T> on first execution: returns both the result list and the
    // resolved RowFactory so CompiledSqlQuery can cache it for all subsequent calls.
    public static async Task<(List<T> Result, RowFactoryCache<T>.RowFactory Factory)> ReadListWithFactoryAsync<T>(
        DbCommand command,
        int capacityHint,
        CancellationToken cancellationToken)
    {
        var connection = command.Connection ?? throw new InvalidOperationException("DbCommand.Connection is null.");
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SingleResult | CommandBehavior.SequentialAccess,
                cancellationToken).ConfigureAwait(false);

            var factory = RowFactoryCache<T>.GetOrAdd(reader, command.CommandText);
            var result = await ReadAllAsync(reader, factory, capacityHint, cancellationToken).ConfigureAwait(false);
            return (result, factory);
        }
        finally
        {
            if (shouldClose && connection.State != ConnectionState.Closed)
                await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    // Called by CompiledSqlQuery<T> on first execution for single-row queries.
    public static async Task<(T Result, RowFactoryCache<T>.RowFactory Factory)> ReadFirstOrDefaultWithFactoryAsync<T>(
        DbCommand command,
        CancellationToken cancellationToken)
    {
        var connection = command.Connection ?? throw new InvalidOperationException("DbCommand.Connection is null.");
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SingleResult | CommandBehavior.SingleRow | CommandBehavior.SequentialAccess,
                cancellationToken).ConfigureAwait(false);

            var factory = RowFactoryCache<T>.GetOrAdd(reader, command.CommandText);
            T result;

            if (!cancellationToken.CanBeCanceled)
            {
                if (reader is SqlDataReader sqlReader && factory.Sql is { } sqlFactory)
                    result = sqlReader.Read() ? sqlFactory(sqlReader) : default!;
                else
                {
                    var gen = factory.General;
                    result = reader.Read() ? gen(reader) : default!;
                }
            }
            else
            {
                if (reader is SqlDataReader sqlReader2 && factory.Sql is { } sqlFactory2)
                    result = await sqlReader2.ReadAsync(cancellationToken).ConfigureAwait(false) ? sqlFactory2(sqlReader2) : default!;
                else
                {
                    var gen = factory.General;
                    result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? gen(reader) : default!;
                }
            }

            return (result, factory);
        }
        finally
        {
            if (shouldClose && connection.State != ConnectionState.Closed)
                await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    // Called by CompiledSqlQuery<T> when it already has a cached factory — skips GetOrAdd entirely.
    public static async Task<List<T>> ReadListAsync<T>(
        DbCommand command,
        RowFactoryCache<T>.RowFactory factory,
        int capacityHint,
        CancellationToken cancellationToken)
    {
        var connection = command.Connection ?? throw new InvalidOperationException("DbCommand.Connection is null.");
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SingleResult | CommandBehavior.SequentialAccess,
                cancellationToken).ConfigureAwait(false);

            return await ReadAllAsync(reader, factory, capacityHint, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (shouldClose && connection.State != ConnectionState.Closed)
                await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    public static async Task<T> ReadFirstOrDefaultAsync<T>(DbCommand command, CancellationToken cancellationToken)
    {
        var connection = command.Connection ?? throw new InvalidOperationException("DbCommand.Connection is null.");
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SingleResult | CommandBehavior.SingleRow | CommandBehavior.SequentialAccess,
                cancellationToken).ConfigureAwait(false);
            var factory = RowFactoryCache<T>.GetOrAdd(reader, command.CommandText);

            if (!cancellationToken.CanBeCanceled)
            {
                if (reader is SqlDataReader sqlReader && factory.Sql is { } sqlFactory)
                    return sqlReader.Read() ? sqlFactory(sqlReader) : default!;

                var generalFactory = factory.General;
                return reader.Read() ? generalFactory(reader) : default!;
            }

            if (reader is SqlDataReader sqlReader2 && factory.Sql is { } sqlFactory2)
                return await sqlReader2.ReadAsync(cancellationToken).ConfigureAwait(false) ? sqlFactory2(sqlReader2) : default!;

            var generalFactory2 = factory.General;
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? generalFactory2(reader) : default!;
        }
        finally
        {
            if (shouldClose && connection.State != ConnectionState.Closed)
                await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    // Called by CompiledSqlQuery<T> when it already has a cached factory.
    public static async Task<T> ReadFirstOrDefaultAsync<T>(
        DbCommand command,
        RowFactoryCache<T>.RowFactory factory,
        CancellationToken cancellationToken)
    {
        var connection = command.Connection ?? throw new InvalidOperationException("DbCommand.Connection is null.");
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SingleResult | CommandBehavior.SingleRow | CommandBehavior.SequentialAccess,
                cancellationToken).ConfigureAwait(false);

            if (!cancellationToken.CanBeCanceled)
            {
                if (reader is SqlDataReader sqlReader && factory.Sql is { } sqlFactory)
                    return sqlReader.Read() ? sqlFactory(sqlReader) : default!;

                var generalFactory = factory.General;
                return reader.Read() ? generalFactory(reader) : default!;
            }

            if (reader is SqlDataReader sqlReader2 && factory.Sql is { } sqlFactory2)
                return await sqlReader2.ReadAsync(cancellationToken).ConfigureAwait(false) ? sqlFactory2(sqlReader2) : default!;

            var generalFactory2 = factory.General;
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? generalFactory2(reader) : default!;
        }
        finally
        {
            if (shouldClose && connection.State != ConnectionState.Closed)
                await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    // Streaming: yields rows one-by-one without buffering into a List<T>.
    // Called by DbContextExtensions.SelectSqlQueryStreamAsync — lets callers process huge result
    // sets row-by-row without holding the entire result set in memory.
    public static async IAsyncEnumerable<T> StreamAsync<T>(
        DbCommand command,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var connection = command.Connection ?? throw new InvalidOperationException("DbCommand.Connection is null.");
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        DbDataReader? reader = null;
        try
        {
            reader = await command.ExecuteReaderAsync(
                CommandBehavior.SingleResult | CommandBehavior.SequentialAccess,
                cancellationToken).ConfigureAwait(false);

            var factory = RowFactoryCache<T>.GetOrAdd(reader, command.CommandText);

            if (reader is SqlDataReader sqlReader && factory.Sql is { } sqlFactory)
            {
                while (await sqlReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    yield return sqlFactory(sqlReader);
            }
            else
            {
                var gen = factory.General;
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    yield return gen(reader);
            }
        }
        finally
        {
            if (reader is not null)
                await reader.DisposeAsync().ConfigureAwait(false);

            if (shouldClose && connection.State != ConnectionState.Closed)
                await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    // Shared hot loop extracted so both overloads of ReadListAsync use identical code.
    // capacityHint = 0 means use DefaultListCapacity (no prior knowledge).
    private static async Task<List<T>> ReadAllAsync<T>(
        DbDataReader reader,
        RowFactoryCache<T>.RowFactory factory,
        int capacityHint,
        CancellationToken cancellationToken)
    {
        var results = new List<T>(capacityHint > 0 ? capacityHint : DefaultListCapacity);

        if (!cancellationToken.CanBeCanceled)
        {
            if (reader is SqlDataReader sqlReader && factory.Sql is { } sqlFactory)
            {
                while (sqlReader.Read())
                    results.Add(sqlFactory(sqlReader));
            }
            else
            {
                var generalFactory = factory.General;
                while (reader.Read())
                    results.Add(generalFactory(reader));
            }
        }
        else
        {
            if (reader is SqlDataReader sqlReader && factory.Sql is { } sqlFactory)
            {
                while (await sqlReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    results.Add(sqlFactory(sqlReader));
            }
            else
            {
                var generalFactory = factory.General;
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    results.Add(generalFactory(reader));
            }
        }

        return results;
    }

    // Made internal so CompiledSqlQuery<T> can call GetOrAdd once and store the result.
    internal static class RowFactoryCache<T>
    {
        private static readonly ConcurrentDictionary<SchemaFingerprint, RowFactory> Cache = new();

        // [ThreadStatic] makes these thread-local: each thread has its own last-seen entry.
        // This eliminates read-write races between concurrent queries on different threads
        // while keeping the zero-allocation fast-path lookup for the single-threaded case.
        [ThreadStatic] private static CacheEntry? Last;
        [ThreadStatic] private static CommandEntry? LastCommand;

        public static RowFactory GetOrAdd(DbDataReader reader, string? commandText)
        {
            // Fast-path 1: same command text + field count (reused DbCommand, same schema).
            if (commandText is not null)
            {
                var lastCommand = LastCommand;
                if (lastCommand.HasValue &&
                    string.Equals(lastCommand.Value.CommandText, commandText, StringComparison.Ordinal) &&
                    lastCommand.Value.FieldCount == reader.FieldCount)
                    return lastCommand.Value.Factory;
            }

            // Fast-path 2: same schema fingerprint as the very last lookup.
            var fingerprint = SchemaFingerprint.Create(reader);

            var last = Last;
            if (last.HasValue && last.Value.Fingerprint.Equals(fingerprint))
            {
                if (commandText is not null)
                    LastCommand = new CommandEntry(commandText, reader.FieldCount, last.Value.Factory);
                return last.Value.Factory;
            }

            // Slow path: dictionary lookup / build.
            if (Cache.TryGetValue(fingerprint, out var cached))
            {
                Last = new CacheEntry(fingerprint, cached);
                if (commandText is not null)
                    LastCommand = new CommandEntry(commandText, reader.FieldCount, cached);
                return cached;
            }

            var created = BuildFactory(reader);
            Cache[fingerprint] = created;
            Last = new CacheEntry(fingerprint, created);
            if (commandText is not null)
                LastCommand = new CommandEntry(commandText, reader.FieldCount, created);
            return created;
        }

        public readonly record struct RowFactory(Func<DbDataReader, T> General, Func<SqlDataReader, T>? Sql);

        private readonly record struct CacheEntry(SchemaFingerprint Fingerprint, RowFactory Factory);
        private readonly record struct CommandEntry(string CommandText, int FieldCount, RowFactory Factory);

        private static RowFactory BuildFactory(DbDataReader reader)
        {
            var columnIsNotNull = TryGetNotNullColumns(reader);

            var general = BuildFactoryCore<DbDataReader>(reader, columnIsNotNull);

            Func<SqlDataReader, T>? sql = null;
            if (reader is SqlDataReader)
                sql = BuildFactoryCore<SqlDataReader>(reader, columnIsNotNull);

            return new RowFactory(general, sql);
        }

        private static bool[]? TryGetNotNullColumns(DbDataReader reader)
        {
            var targetType = typeof(T);
            var ctor = targetType.GetConstructor(Type.EmptyTypes);
            if (ctor is null && targetType.IsValueType is false)
                throw new InvalidOperationException($"Type '{targetType.FullName}' must have a public parameterless constructor.");

            try
            {
                var schema = reader.GetColumnSchema();
                if (schema.Count == reader.FieldCount)
                {
                    var columnIsNotNull = new bool[reader.FieldCount];
                    for (var i = 0; i < columnIsNotNull.Length; i++)
                        columnIsNotNull[i] = schema[i].AllowDBNull is false;
                    return columnIsNotNull;
                }
            }
            catch (NotSupportedException) { }
            catch (NotImplementedException) { }

            return null;
        }

        private static Func<TReader, T> BuildFactoryCore<TReader>(DbDataReader reader, bool[]? columnIsNotNull)
            where TReader : DbDataReader
        {
            var targetType = typeof(T);
            var ctor = targetType.GetConstructor(Type.EmptyTypes);

            var readerParam = Expression.Parameter(typeof(TReader), "reader");
            var instanceVar = Expression.Variable(targetType, "obj");

            var createInstance = targetType.IsValueType
                ? (Expression)Expression.Default(targetType)
                : Expression.New(ctor!);

            var expressions = new List<Expression>(capacity: reader.FieldCount + 2)
            {
                Expression.Assign(instanceVar, createInstance)
            };

            for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
            {
                var columnName = reader.GetName(ordinal);
                if (!TypeMap<T>.Properties.TryGetValue(columnName, out var property))
                    continue;

                var propertyType = property.PropertyType;
                var underlyingPropertyType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
                var fieldType = reader.GetFieldType(ordinal);

                var ordinalExpr = Expression.Constant(ordinal);

                var readValueExpr = CreateReadValueExpression(readerParam, ordinalExpr, underlyingPropertyType, fieldType);
                var assignTarget = Expression.Property(instanceVar, property);

                Expression valueForAssignment = readValueExpr;
                if (valueForAssignment.Type != propertyType)
                    valueForAssignment = Expression.Convert(valueForAssignment, propertyType);

                if (columnIsNotNull is not null && columnIsNotNull[ordinal])
                {
                    expressions.Add(Expression.Assign(assignTarget, valueForAssignment));
                    continue;
                }

                var isDbNullExpr = Expression.Call(readerParam, WellKnownMethods.IsDBNull, ordinalExpr);

                if (!propertyType.IsValueType || Nullable.GetUnderlyingType(propertyType) is not null)
                {
                    var defaultValue = Expression.Default(propertyType);
                    var conditional = Expression.Condition(isDbNullExpr, defaultValue, valueForAssignment);
                    expressions.Add(Expression.Assign(assignTarget, conditional));
                }
                else
                {
                    expressions.Add(Expression.IfThen(Expression.Not(isDbNullExpr), Expression.Assign(assignTarget, valueForAssignment)));
                }
            }

            expressions.Add(instanceVar);

            var body = Expression.Block(new[] { instanceVar }, expressions);
            return Expression.Lambda<Func<TReader, T>>(body, readerParam).Compile();
        }

        private static Expression CreateReadValueExpression(
            ParameterExpression reader,
            ConstantExpression ordinal,
            Type targetType,
            Type fieldType)
        {
            if (targetType == typeof(object))
                return Expression.Call(reader, WellKnownMethods.GetValue, ordinal);

            if (targetType.IsEnum)
                return CreateEnumReadExpression(reader, ordinal, targetType, fieldType);

            if (targetType == typeof(string) && fieldType == typeof(string))
                return Expression.Call(reader, WellKnownMethods.GetString, ordinal);

            if (targetType == fieldType)
            {
                if (WellKnownMethods.TryGetTypedGetter(targetType, out var getter))
                    return Expression.Call(reader, getter, ordinal);

                var getFieldValue = WellKnownMethods.GetFieldValue.MakeGenericMethod(targetType);
                return Expression.Call(reader, getFieldValue, ordinal);
            }

            // Fallback: read boxed value and convert.
            var boxed = Expression.Call(reader, WellKnownMethods.GetValue, ordinal);
            var convert = WellKnownMethods.ConvertTo.MakeGenericMethod(targetType);
            return Expression.Call(convert, boxed);
        }

        private static Expression CreateEnumReadExpression(
            ParameterExpression reader,
            ConstantExpression ordinal,
            Type enumType,
            Type fieldType)
        {
            if (fieldType == typeof(string))
            {
                var strValue = Expression.Call(reader, WellKnownMethods.GetString, ordinal);
                var parse = WellKnownMethods.EnumParse.MakeGenericMethod(enumType);
                return Expression.Call(parse, strValue, Expression.Constant(true));
            }

            var enumUnderlying = Enum.GetUnderlyingType(enumType);

            Expression numericValue;
            if (fieldType == enumUnderlying)
            {
                if (WellKnownMethods.TryGetTypedGetter(enumUnderlying, out var getter))
                {
                    numericValue = Expression.Call(reader, getter, ordinal);
                }
                else
                {
                    var getFieldValue = WellKnownMethods.GetFieldValue.MakeGenericMethod(enumUnderlying);
                    numericValue = Expression.Call(reader, getFieldValue, ordinal);
                }
            }
            else
            {
                var boxed = Expression.Call(reader, WellKnownMethods.GetValue, ordinal);
                var convert = WellKnownMethods.ConvertTo.MakeGenericMethod(enumUnderlying);
                numericValue = Expression.Call(convert, boxed);
            }

            return Expression.Convert(numericValue, enumType);
        }

        private static class TypeMap<TType>
        {
            public static readonly IReadOnlyDictionary<string, PropertyInfo> Properties = BuildPropertyMap();

            private static IReadOnlyDictionary<string, PropertyInfo> BuildPropertyMap()
            {
                var type = typeof(TType);
                var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var dict = new Dictionary<string, PropertyInfo>(props.Length, StringComparer.OrdinalIgnoreCase);

                foreach (var prop in props)
                {
                    if (prop.SetMethod is null || prop.SetMethod.IsPublic is false)
                        continue;

                    dict[prop.Name] = prop;
                }

                return dict;
            }
        }

        private readonly record struct SchemaFingerprint(int FieldCount, ulong Hash1, ulong Hash2)
        {
            private const ulong FnvOffsetBasis1 = 14695981039346656037UL;
            private const ulong FnvOffsetBasis2 = 9650029242287828579UL;
            private const ulong FnvPrime = 1099511628211UL;

            public static SchemaFingerprint Create(DbDataReader reader)
            {
                var hash1 = FnvOffsetBasis1;
                var hash2 = FnvOffsetBasis2;
                var fieldCount = reader.FieldCount;
                for (var i = 0; i < fieldCount; i++)
                {
                    var name = reader.GetName(i);

                    // hash1: name-then-type (position-order sensitive via sequential XOR)
                    hash1 = AddStringOrdinalIgnoreCase(hash1, name);

                    var typeHandle = reader.GetFieldType(i).TypeHandle.Value.ToInt64();
                    hash1 = AddInt64(hash1, typeHandle);

                    // hash2: mix in ordinal FIRST, making it independent from hash1.
                    // Two schemas with the same columns in different order will diverge in hash2
                    // even if hash1 were to accidentally collide.
                    hash2 ^= (ulong)i * FnvPrime;
                    hash2 = AddStringOrdinalIgnoreCase(hash2, name);
                    hash2 = AddInt64(hash2, typeHandle);
                }

                return new SchemaFingerprint(fieldCount, hash1, hash2);
            }

            private static ulong AddStringOrdinalIgnoreCase(ulong hash, string value)
            {
                foreach (var ch in value)
                {
                    // ASCII fast-path: letters a-z and A-Z differ only in bit 5.
                    // Clear bit 5 (0x20) to normalise to uppercase without a table lookup.
                    // For non-ASCII characters fall back to char.ToUpperInvariant.
                    var upper = (uint)ch;
                    if ((upper - 'a') <= ('z' - 'a'))
                        upper &= ~0x20u;
                    else if (upper > 127)
                        upper = char.ToUpperInvariant(ch);

                    hash ^= upper;
                    hash *= FnvPrime;
                }
                hash ^= 0;
                hash *= FnvPrime;
                return hash;
            }

            private static ulong AddInt64(ulong hash, long value)
            {
                unchecked
                {
                    hash ^= (byte)value; hash *= FnvPrime;
                    hash ^= (byte)(value >> 8); hash *= FnvPrime;
                    hash ^= (byte)(value >> 16); hash *= FnvPrime;
                    hash ^= (byte)(value >> 24); hash *= FnvPrime;
                    hash ^= (byte)(value >> 32); hash *= FnvPrime;
                    hash ^= (byte)(value >> 40); hash *= FnvPrime;
                    hash ^= (byte)(value >> 48); hash *= FnvPrime;
                    hash ^= (byte)(value >> 56); hash *= FnvPrime;
                    return hash;
                }
            }
        }

        private static class WellKnownMethods
        {
            public static readonly MethodInfo IsDBNull =
                typeof(DbDataReader).GetMethod(nameof(DbDataReader.IsDBNull), [typeof(int)])!;

            public static readonly MethodInfo GetValue =
                typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetValue), [typeof(int)])!;

            public static readonly MethodInfo GetString =
                typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetString), [typeof(int)])!;

            public static readonly MethodInfo GetFieldValue = typeof(DbDataReader)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(m => m.Name == nameof(DbDataReader.GetFieldValue) && m.IsGenericMethodDefinition);

            public static readonly MethodInfo ConvertTo = typeof(WellKnownMethods)
                .GetMethod(nameof(ConvertToImpl), BindingFlags.NonPublic | BindingFlags.Static)!;

            public static readonly MethodInfo EnumParse = typeof(Enum)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m =>
                    m.Name == nameof(Enum.Parse) &&
                    m.IsGenericMethodDefinition &&
                    m.GetParameters() is { Length: 2 } p &&
                    p[0].ParameterType == typeof(string) &&
                    p[1].ParameterType == typeof(bool));

            private static readonly IReadOnlyDictionary<Type, MethodInfo> TypedGetters = new Dictionary<Type, MethodInfo>
            {
                [typeof(bool)]           = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetBoolean),  [typeof(int)])!,
                [typeof(byte)]           = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetByte),     [typeof(int)])!,
                [typeof(short)]          = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetInt16),    [typeof(int)])!,
                [typeof(int)]            = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetInt32),    [typeof(int)])!,
                [typeof(long)]           = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetInt64),    [typeof(int)])!,
                [typeof(float)]          = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetFloat),    [typeof(int)])!,
                [typeof(double)]         = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetDouble),   [typeof(int)])!,
                [typeof(decimal)]        = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetDecimal),  [typeof(int)])!,
                [typeof(Guid)]           = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetGuid),     [typeof(int)])!,
                [typeof(DateTime)]       = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetDateTime), [typeof(int)])!,
                [typeof(char)]           = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetChar),     [typeof(int)])!,
                // DateTimeOffset: SQL Server datetimeoffset type — use GetFieldValue<DateTimeOffset> at runtime.
                // Registered so CreateReadValueExpression falls through to GetFieldValue<T> instead of boxed GetValue.
                [typeof(DateTimeOffset)] = typeof(DbDataReader)
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Single(m => m.Name == nameof(DbDataReader.GetFieldValue) && m.IsGenericMethodDefinition)
                    .MakeGenericMethod(typeof(DateTimeOffset)),
            };

            public static bool TryGetTypedGetter(Type type, out MethodInfo getter) =>
                TypedGetters.TryGetValue(type, out getter!);

            private static TTarget ConvertToImpl<TTarget>(object value)
            {
                if (value is TTarget t)
                    return t;

                var targetType = typeof(TTarget);

                if (targetType == typeof(Guid) && value is string s)
                    return (TTarget)(object)Guid.Parse(s);

                if (targetType == typeof(DateOnly) && value is DateTime dt)
                    return (TTarget)(object)DateOnly.FromDateTime(dt);

                if (targetType == typeof(TimeOnly) && value is TimeSpan ts)
                    return (TTarget)(object)TimeOnly.FromTimeSpan(ts);

                if (targetType == typeof(DateTimeOffset) && value is DateTime dts)
                    return (TTarget)(object)new DateTimeOffset(dts);

                return (TTarget)Convert.ChangeType(value, targetType);
            }
        }
    }
}
