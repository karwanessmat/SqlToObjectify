using System.Collections.Concurrent;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

public static class FastSqlQueryMapper
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, PropertySetter>> SetterCache = new();

    public static async Task<List<T>> SelectSqlQueryListFastAsync<T>(
        this DbContext dbContext,
        string sqlQuery,
        IReadOnlyDictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
        where T : new()
    {
        var connection = dbContext.Database.GetDbConnection();

        await using var command = connection.CreateCommand();
        command.CommandText = sqlQuery;

        if (parameters is not null)
        {
            foreach (var (key, value) in parameters)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@" + key;
                parameter.Value = value ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }
        }

        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var ordinals = GetOrdinals(reader);
        var setters = GetSetters<T>();
        var mappings = BuildMappings(ordinals, setters);

        var results = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var item = new T();
            foreach (var (ordinal, setter) in mappings)
            {
                var value = reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
                setter.Set(item, value);
            }

            results.Add(item);
        }

        return results;
    }

    private static IReadOnlyDictionary<string, int> GetOrdinals(DbDataReader reader)
    {
        var fieldCount = reader.FieldCount;
        var map = new Dictionary<string, int>(fieldCount, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < fieldCount; i++)
            map[reader.GetName(i)] = i;
        return map;
    }

    private static IReadOnlyDictionary<string, PropertySetter> GetSetters<T>() where T : new()
        => SetterCache.GetOrAdd(typeof(T), static _ => BuildSetters(typeof(T)));

    private static IReadOnlyDictionary<string, PropertySetter> BuildSetters(Type type)
    {
        var dict = new Dictionary<string, PropertySetter>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanWrite || prop.SetMethod is null)
                continue;

            dict[prop.Name] = new PropertySetter(prop.PropertyType, CreateSetter(type, prop), prop);
        }

        return dict;
    }

    private static Action<object, object?> CreateSetter(Type declaringType, PropertyInfo property)
    {
        var target = Expression.Parameter(typeof(object), "target");
        var value = Expression.Parameter(typeof(object), "value");

        var typedTarget = Expression.Convert(target, declaringType);

        var propertyType = property.PropertyType;
        var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        Expression convertedValue;
        if (underlyingType.IsEnum)
        {
            var parseEnum = typeof(FastSqlQueryMapper).GetMethod(nameof(ParseEnum), BindingFlags.NonPublic | BindingFlags.Static)!;
            var call = Expression.Call(parseEnum, Expression.Constant(underlyingType, typeof(Type)), value);
            convertedValue = Expression.Convert(call, underlyingType);
        }
        else
        {
            var convert = typeof(FastSqlQueryMapper).GetMethod(nameof(ConvertValue), BindingFlags.NonPublic | BindingFlags.Static)!;
            var call = Expression.Call(convert, Expression.Constant(underlyingType, typeof(Type)), value);
            convertedValue = Expression.Convert(call, underlyingType);
        }

        if (Nullable.GetUnderlyingType(propertyType) is not null)
        {
            convertedValue = Expression.Condition(
                Expression.Equal(value, Expression.Constant(null, typeof(object))),
                Expression.Default(propertyType),
                Expression.Convert(convertedValue, propertyType));
        }

        var assign = Expression.Assign(Expression.Property(typedTarget, property), convertedValue);
        return Expression.Lambda<Action<object, object?>>(assign, target, value).Compile();
    }

    private static object? ConvertValue(Type targetType, object? value)
    {
        if (value is null)
            return null;

        if (targetType.IsInstanceOfType(value))
            return value;

        return Convert.ChangeType(value, targetType);
    }

    private static object? ParseEnum(Type enumType, object? value)
    {
        if (value is null)
            return null;

        if (value is string s)
            return Enum.Parse(enumType, s, ignoreCase: true);

        var underlying = Enum.GetUnderlyingType(enumType);
        var numeric = Convert.ChangeType(value, underlying);
        return Enum.ToObject(enumType, numeric!);
    }

    private static List<(int Ordinal, PropertySetter Setter)> BuildMappings(
        IReadOnlyDictionary<string, int> ordinals,
        IReadOnlyDictionary<string, PropertySetter> setters)
    {
        var mappings = new List<(int, PropertySetter)>();
        foreach (var (name, ordinal) in ordinals)
        {
            if (setters.TryGetValue(name, out var setter))
                mappings.Add((ordinal, setter));
        }

        return mappings;
    }

    private sealed record PropertySetter(Type PropertyType, Action<object, object?> Set, PropertyInfo Property);
}

