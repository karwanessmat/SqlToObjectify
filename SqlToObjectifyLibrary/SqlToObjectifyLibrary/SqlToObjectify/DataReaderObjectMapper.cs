using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;

namespace SqlToObjectify;

internal static class DataReaderObjectMapper
{
    public static async Task<List<T>> ReadListAsync<T>(DbCommand command, CancellationToken cancellationToken)
    {
        var connection = command.Connection ?? throw new InvalidOperationException("DbCommand.Connection is null.");
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleResult, cancellationToken).ConfigureAwait(false);
            var factory = RowFactoryCache<T>.GetOrAdd(reader);

            var results = new List<T>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                results.Add(factory(reader));

            return results;
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
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleResult | CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false);
            var factory = RowFactoryCache<T>.GetOrAdd(reader);

            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? factory(reader) : default!;
        }
        finally
        {
            if (shouldClose && connection.State != ConnectionState.Closed)
                await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    private static class RowFactoryCache<T>
    {
        private static readonly ConcurrentDictionary<SchemaFingerprint, Func<DbDataReader, T>> Cache = new();
        private static CacheEntry? Last;

        public static Func<DbDataReader, T> GetOrAdd(DbDataReader reader)
        {
            var fingerprint = SchemaFingerprint.Create(reader);

            var last = Last;
            if (last.HasValue && last.Value.Fingerprint.Equals(fingerprint))
                return last.Value.Factory;

            if (Cache.TryGetValue(fingerprint, out var cached))
            {
                Last = new CacheEntry(fingerprint, cached);
                return cached;
            }

            var created = BuildFactory(reader);
            Cache[fingerprint] = created;
            Last = new CacheEntry(fingerprint, created);
            return created;
        }

        private readonly record struct CacheEntry(SchemaFingerprint Fingerprint, Func<DbDataReader, T> Factory);

        private static Func<DbDataReader, T> BuildFactory(DbDataReader reader)
        {
            var targetType = typeof(T);
            var ctor = targetType.GetConstructor(Type.EmptyTypes);
            if (ctor is null && targetType.IsValueType is false)
                throw new InvalidOperationException($"Type '{targetType.FullName}' must have a public parameterless constructor.");

            bool[]? columnIsNotNull = null;
            try
            {
                var schema = reader.GetColumnSchema();
                if (schema.Count == reader.FieldCount)
                {
                    columnIsNotNull = new bool[reader.FieldCount];
                    for (var i = 0; i < columnIsNotNull.Length; i++)
                        columnIsNotNull[i] = schema[i].AllowDBNull is false;
                }
            }
            catch (NotSupportedException)
            {
                // Some providers don't support GetColumnSchema().
            }
            catch (NotImplementedException)
            {
                // Some providers throw NotImplementedException for schema APIs.
            }

            var readerParam = Expression.Parameter(typeof(DbDataReader), "reader");
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
                    // Keep existing behavior: if DB null and property is non-nullable value type, skip setting it.
                    expressions.Add(Expression.IfThen(Expression.Not(isDbNullExpr), Expression.Assign(assignTarget, valueForAssignment)));
                }
            }

            expressions.Add(instanceVar);

            var body = Expression.Block(new[] { instanceVar }, expressions);
            return Expression.Lambda<Func<DbDataReader, T>>(body, readerParam).Compile();
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

            // Fallback: read boxed value and convert (keeps old Convert.ChangeType behavior for mismatched types).
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
                var getString = WellKnownMethods.GetString;
                var strValue = Expression.Call(reader, getString, ordinal);
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
                    hash1 = AddStringOrdinalIgnoreCase(hash1, name);
                    hash2 = AddStringOrdinalIgnoreCase(hash2, name);

                    var typeHandle = reader.GetFieldType(i).TypeHandle.Value.ToInt64();
                    hash1 = AddInt64(hash1, typeHandle);
                    hash2 = AddInt64(hash2, typeHandle);
                }

                return new SchemaFingerprint(fieldCount, hash1, hash2);
            }

            private static ulong AddStringOrdinalIgnoreCase(ulong hash, string value)
            {
                foreach (var ch in value)
                {
                    var normalized = char.ToUpperInvariant(ch);
                    hash ^= normalized;
                    hash *= FnvPrime;
                }

                // separator
                hash ^= 0;
                hash *= FnvPrime;
                return hash;
            }

            private static ulong AddInt64(ulong hash, long value)
            {
                unchecked
                {
                    hash ^= (byte)value;
                    hash *= FnvPrime;
                    hash ^= (byte)(value >> 8);
                    hash *= FnvPrime;
                    hash ^= (byte)(value >> 16);
                    hash *= FnvPrime;
                    hash ^= (byte)(value >> 24);
                    hash *= FnvPrime;
                    hash ^= (byte)(value >> 32);
                    hash *= FnvPrime;
                    hash ^= (byte)(value >> 40);
                    hash *= FnvPrime;
                    hash ^= (byte)(value >> 48);
                    hash *= FnvPrime;
                    hash ^= (byte)(value >> 56);
                    hash *= FnvPrime;
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
                [typeof(bool)] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetBoolean), [typeof(int)])!,
                [typeof(byte)] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetByte), [typeof(int)])!,
                [typeof(short)] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetInt16), [typeof(int)])!,
                [typeof(int)] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetInt32), [typeof(int)])!,
                [typeof(long)] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetInt64), [typeof(int)])!,
                [typeof(float)] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetFloat), [typeof(int)])!,
                [typeof(double)] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetDouble), [typeof(int)])!,
                [typeof(decimal)] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetDecimal), [typeof(int)])!,
                [typeof(Guid)] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetGuid), [typeof(int)])!,
                [typeof(DateTime)] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetDateTime), [typeof(int)])!,
                [typeof(char)] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetChar), [typeof(int)])!,
            };

            public static bool TryGetTypedGetter(Type type, out MethodInfo getter) => TypedGetters.TryGetValue(type, out getter!);

            private static TTarget ConvertToImpl<TTarget>(object value)
            {
                if (value is TTarget t)
                    return t;

                var targetType = typeof(TTarget);

                if (targetType == typeof(Guid))
                {
                    if (value is string s)
                        return (TTarget)(object)Guid.Parse(s);
                }

                if (targetType == typeof(DateOnly))
                {
                    if (value is DateTime dt)
                        return (TTarget)(object)DateOnly.FromDateTime(dt);
                }

                if (targetType == typeof(TimeOnly))
                {
                    if (value is TimeSpan ts)
                        return (TTarget)(object)TimeOnly.FromTimeSpan(ts);
                }

                return (TTarget)Convert.ChangeType(value, targetType);
            }
        }
    }
}
