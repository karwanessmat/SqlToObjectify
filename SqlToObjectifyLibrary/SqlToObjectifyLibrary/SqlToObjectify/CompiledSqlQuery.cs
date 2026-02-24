using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace SqlToObjectify;

/// <summary>
/// A pre-compiled SQL query that reuses a single <see cref="DbCommand"/> and caches the
/// row-mapping delegate after the first execution. Fastest path for repeated hot queries.
/// </summary>
public sealed class CompiledSqlQuery<T> : IAsyncDisposable, IDisposable
{
    private readonly DbCommand _command;
    private readonly string[] _parameterNames;
    private readonly DbParameter?[] _parameters;

    // Per-slot SqlDbType: inferred once at SetParameter-time, never re-inferred afterwards.
    // SqlDbType.Variant (0) means "not yet set".
    private readonly SqlDbType[] _sqlDbTypes;
    private readonly int[] _stringSizes; // for NVarChar: 4000 or -1 (MAX)

    // Cached after the very first ToListAsync / FirstOrDefaultAsync call.
    // Once set, every subsequent call skips GetOrAdd entirely.
    private DataReaderObjectMapper.RowFactoryCache<T>.RowFactory? _cachedFactory;

    // Tracks the row count from the last ToListAsync call.
    // Used to pre-size the List<T> on the next call, eliminating mid-iteration reallocation noise.
    private int _lastCount;

    private bool _disposed;

    internal CompiledSqlQuery(
        DbConnection connection,
        string sqlQuery,
        CommandType commandType,
        IReadOnlyList<string> parameterNames)
    {
        _command = connection.CreateCommand();
        _command.CommandText = sqlQuery;
        _command.CommandType = commandType;

        var count = parameterNames.Count;
        if (count == 0)
        {
            _parameterNames = [];
            _parameters = [];
            _sqlDbTypes = [];
            _stringSizes = [];
            return;
        }

        _parameterNames = new string[count];
        _parameters = new DbParameter?[count];
        _sqlDbTypes = new SqlDbType[count]; // default 0 = SqlDbType.BigInt — overwritten on first SetParameter
        _stringSizes = new int[count];

        for (var i = 0; i < count; i++)
        {
            var name = parameterNames[i];
            _parameterNames[i] = name.StartsWith("@", StringComparison.Ordinal) ? name : "@" + name;
            _sqlDbTypes[i] = (SqlDbType)(-1); // sentinel: not yet configured
            _stringSizes[i] = -2;              // sentinel: not yet configured
        }
    }

    public int ParameterCount => _parameterNames.Length;

    // -------------------------------------------------------------------------
    // Typed SetParameter overloads — zero boxing for the most common CLR types.
    // -------------------------------------------------------------------------

    public void SetParameter(int index, int value)
    {
        ThrowIfDisposed();
        var p = GetOrCreateSqlParameter(index);
        if (p is SqlParameter sp)
        {
            if (_sqlDbTypes[index] != SqlDbType.Int)
            {
                sp.SqlDbType = SqlDbType.Int;
                _sqlDbTypes[index] = SqlDbType.Int;
            }
            sp.Value = value;
        }
        else
        {
            p.DbType = DbType.Int32;
            p.Value = value;
        }
    }

    public void SetParameter(int index, long value)
    {
        ThrowIfDisposed();
        var p = GetOrCreateSqlParameter(index);
        if (p is SqlParameter sp)
        {
            if (_sqlDbTypes[index] != SqlDbType.BigInt)
            {
                sp.SqlDbType = SqlDbType.BigInt;
                _sqlDbTypes[index] = SqlDbType.BigInt;
            }
            sp.Value = value;
        }
        else
        {
            p.DbType = DbType.Int64;
            p.Value = value;
        }
    }

    public void SetParameter(int index, short value)
    {
        ThrowIfDisposed();
        var p = GetOrCreateSqlParameter(index);
        if (p is SqlParameter sp)
        {
            if (_sqlDbTypes[index] != SqlDbType.SmallInt)
            {
                sp.SqlDbType = SqlDbType.SmallInt;
                _sqlDbTypes[index] = SqlDbType.SmallInt;
            }
            sp.Value = value;
        }
        else
        {
            p.DbType = DbType.Int16;
            p.Value = value;
        }
    }

    public void SetParameter(int index, bool value)
    {
        ThrowIfDisposed();
        var p = GetOrCreateSqlParameter(index);
        if (p is SqlParameter sp)
        {
            if (_sqlDbTypes[index] != SqlDbType.Bit)
            {
                sp.SqlDbType = SqlDbType.Bit;
                _sqlDbTypes[index] = SqlDbType.Bit;
            }
            sp.Value = value;
        }
        else
        {
            p.DbType = DbType.Boolean;
            p.Value = value;
        }
    }

    public void SetParameter(int index, Guid value)
    {
        ThrowIfDisposed();
        var p = GetOrCreateSqlParameter(index);
        if (p is SqlParameter sp)
        {
            if (_sqlDbTypes[index] != SqlDbType.UniqueIdentifier)
            {
                sp.SqlDbType = SqlDbType.UniqueIdentifier;
                _sqlDbTypes[index] = SqlDbType.UniqueIdentifier;
            }
            sp.Value = value;
        }
        else
        {
            p.DbType = DbType.Guid;
            p.Value = value;
        }
    }

    public void SetParameter(int index, DateTime value)
    {
        ThrowIfDisposed();
        var p = GetOrCreateSqlParameter(index);
        if (p is SqlParameter sp)
        {
            if (_sqlDbTypes[index] != SqlDbType.DateTime2)
            {
                sp.SqlDbType = SqlDbType.DateTime2;
                _sqlDbTypes[index] = SqlDbType.DateTime2;
            }
            sp.Value = value;
        }
        else
        {
            p.DbType = DbType.DateTime2;
            p.Value = value;
        }
    }

    public void SetParameter(int index, DateTimeOffset value)
    {
        ThrowIfDisposed();
        var p = GetOrCreateSqlParameter(index);
        if (p is SqlParameter sp)
        {
            if (_sqlDbTypes[index] != SqlDbType.DateTimeOffset)
            {
                sp.SqlDbType = SqlDbType.DateTimeOffset;
                _sqlDbTypes[index] = SqlDbType.DateTimeOffset;
            }
            sp.Value = value;
        }
        else
        {
            p.DbType = DbType.DateTimeOffset;
            p.Value = value;
        }
    }

    public void SetParameter(int index, decimal value)
    {
        ThrowIfDisposed();
        var p = GetOrCreateSqlParameter(index);
        if (p is SqlParameter sp)
        {
            if (_sqlDbTypes[index] != SqlDbType.Decimal)
            {
                sp.SqlDbType = SqlDbType.Decimal;
                _sqlDbTypes[index] = SqlDbType.Decimal;
            }
            sp.Value = value;
        }
        else
        {
            p.DbType = DbType.Decimal;
            p.Value = value;
        }
    }

    public void SetParameter(int index, double value)
    {
        ThrowIfDisposed();
        var p = GetOrCreateSqlParameter(index);
        if (p is SqlParameter sp)
        {
            if (_sqlDbTypes[index] != SqlDbType.Float)
            {
                sp.SqlDbType = SqlDbType.Float;
                _sqlDbTypes[index] = SqlDbType.Float;
            }
            sp.Value = value;
        }
        else
        {
            p.DbType = DbType.Double;
            p.Value = value;
        }
    }

    public void SetParameter(int index, string? value)
    {
        ThrowIfDisposed();
        var p = GetOrCreateSqlParameter(index);
        if (p is SqlParameter sp)
        {
            if (_sqlDbTypes[index] != SqlDbType.NVarChar)
            {
                sp.SqlDbType = SqlDbType.NVarChar;
                _sqlDbTypes[index] = SqlDbType.NVarChar;
            }

            // Fix size once; thereafter only change if the length bucket changes.
            var desiredSize = value is null ? 4000 : value.Length <= 4000 ? 4000 : -1;
            if (_stringSizes[index] != desiredSize)
            {
                sp.Size = desiredSize;
                _stringSizes[index] = desiredSize;
            }

            sp.Value = value is null ? DBNull.Value : (object)value;
        }
        else
        {
            p.DbType = DbType.String;
            p.Value = value is null ? DBNull.Value : (object)value;
        }
    }

    // Fallback object overload — covers nullable types and any uncommon CLR types.
    public void SetParameter(int index, object? value)
    {
        ThrowIfDisposed();
        var parameter = GetOrCreateParameter(index);

        if (value is null)
        {
            parameter.Value = DBNull.Value;
            return;
        }

        ConfigureParameterFromObject(parameter, index, value);
        parameter.Value = value;
    }

    // -------------------------------------------------------------------------
    // Execute
    // -------------------------------------------------------------------------

    public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        List<T> result;

        if (_cachedFactory.HasValue)
        {
            result = await DataReaderObjectMapper.ReadListAsync(_command, _cachedFactory.Value, _lastCount, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            // First execution: use the full path to resolve and cache the RowFactory.
            var (r, factory) = await DataReaderObjectMapper.ReadListWithFactoryAsync<T>(_command, _lastCount, cancellationToken)
                .ConfigureAwait(false);
            _cachedFactory = factory;
            result = r;
        }

        _lastCount = result.Count;
        return result;
    }

    public async Task<T> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_cachedFactory.HasValue)
            return await DataReaderObjectMapper.ReadFirstOrDefaultAsync(_command, _cachedFactory.Value, cancellationToken)
                .ConfigureAwait(false);

        var (result, factory) = await DataReaderObjectMapper.ReadFirstOrDefaultWithFactoryAsync<T>(_command, cancellationToken)
            .ConfigureAwait(false);

        _cachedFactory = factory;
        return result;
    }

    // -------------------------------------------------------------------------
    // Dispose
    // -------------------------------------------------------------------------

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _command.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return default;
        _disposed = true;
        return _command.DisposeAsync();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(CompiledSqlQuery<T>));
    }

    // Returns an existing or newly created SqlParameter (preferred) or DbParameter for the slot.
    private DbParameter GetOrCreateSqlParameter(int index)
    {
        var parameter = _parameters[index];
        if (parameter is not null)
            return parameter;

        parameter = _command.CreateParameter();
        parameter.ParameterName = _parameterNames[index];
        _command.Parameters.Add(parameter);
        _parameters[index] = parameter;
        return parameter;
    }

    // Same but for the object-value fallback path where we don't want the overloads to conflict.
    private DbParameter GetOrCreateParameter(int index) => GetOrCreateSqlParameter(index);

    private void ConfigureParameterFromObject(DbParameter parameter, int index, object value)
    {
        if (parameter is SqlParameter sqlParameter)
        {
            ConfigureSqlParameterFromObject(sqlParameter, index, value);
            return;
        }

        switch (value)
        {
            case int:     parameter.DbType = DbType.Int32;    break;
            case long:    parameter.DbType = DbType.Int64;    break;
            case short:   parameter.DbType = DbType.Int16;    break;
            case byte:    parameter.DbType = DbType.Byte;     break;
            case bool:    parameter.DbType = DbType.Boolean;  break;
            case Guid:    parameter.DbType = DbType.Guid;     break;
            case DateTime:parameter.DbType = DbType.DateTime2;break;
            case decimal: parameter.DbType = DbType.Decimal;  break;
            case double:  parameter.DbType = DbType.Double;   break;
            case float:   parameter.DbType = DbType.Single;   break;
            case string s:
                parameter.DbType = DbType.String;
                TrySetSize(parameter, s.Length);
                break;
            case byte[] bytes:
                parameter.DbType = DbType.Binary;
                TrySetSize(parameter, bytes.Length);
                break;
        }
    }

    private void ConfigureSqlParameterFromObject(SqlParameter parameter, int index, object value)
    {
        switch (value)
        {
            case int:
                if (_sqlDbTypes[index] != SqlDbType.Int) { parameter.SqlDbType = SqlDbType.Int; _sqlDbTypes[index] = SqlDbType.Int; }
                break;
            case long:
                if (_sqlDbTypes[index] != SqlDbType.BigInt) { parameter.SqlDbType = SqlDbType.BigInt; _sqlDbTypes[index] = SqlDbType.BigInt; }
                break;
            case short:
                if (_sqlDbTypes[index] != SqlDbType.SmallInt) { parameter.SqlDbType = SqlDbType.SmallInt; _sqlDbTypes[index] = SqlDbType.SmallInt; }
                break;
            case byte:
                if (_sqlDbTypes[index] != SqlDbType.TinyInt) { parameter.SqlDbType = SqlDbType.TinyInt; _sqlDbTypes[index] = SqlDbType.TinyInt; }
                break;
            case bool:
                if (_sqlDbTypes[index] != SqlDbType.Bit) { parameter.SqlDbType = SqlDbType.Bit; _sqlDbTypes[index] = SqlDbType.Bit; }
                break;
            case Guid:
                if (_sqlDbTypes[index] != SqlDbType.UniqueIdentifier) { parameter.SqlDbType = SqlDbType.UniqueIdentifier; _sqlDbTypes[index] = SqlDbType.UniqueIdentifier; }
                break;
            case DateTime:
                if (_sqlDbTypes[index] != SqlDbType.DateTime2) { parameter.SqlDbType = SqlDbType.DateTime2; _sqlDbTypes[index] = SqlDbType.DateTime2; }
                break;
            case DateTimeOffset:
                if (_sqlDbTypes[index] != SqlDbType.DateTimeOffset) { parameter.SqlDbType = SqlDbType.DateTimeOffset; _sqlDbTypes[index] = SqlDbType.DateTimeOffset; }
                break;
            case decimal:
                if (_sqlDbTypes[index] != SqlDbType.Decimal) { parameter.SqlDbType = SqlDbType.Decimal; _sqlDbTypes[index] = SqlDbType.Decimal; }
                break;
            case double:
                if (_sqlDbTypes[index] != SqlDbType.Float) { parameter.SqlDbType = SqlDbType.Float; _sqlDbTypes[index] = SqlDbType.Float; }
                break;
            case float:
                if (_sqlDbTypes[index] != SqlDbType.Real) { parameter.SqlDbType = SqlDbType.Real; _sqlDbTypes[index] = SqlDbType.Real; }
                break;
            case string s:
                if (_sqlDbTypes[index] != SqlDbType.NVarChar) { parameter.SqlDbType = SqlDbType.NVarChar; _sqlDbTypes[index] = SqlDbType.NVarChar; }
                var desiredSize = s.Length <= 4000 ? 4000 : -1;
                if (_stringSizes[index] != desiredSize) { parameter.Size = desiredSize; _stringSizes[index] = desiredSize; }
                break;
            case byte[] bytes:
                if (_sqlDbTypes[index] != SqlDbType.VarBinary) { parameter.SqlDbType = SqlDbType.VarBinary; _sqlDbTypes[index] = SqlDbType.VarBinary; }
                var desiredBinarySize = bytes.Length <= 8000 ? bytes.Length : -1;
                if (_stringSizes[index] != desiredBinarySize) { parameter.Size = desiredBinarySize; _stringSizes[index] = desiredBinarySize; }
                break;
        }
    }

    private static void TrySetSize(DbParameter parameter, int size)
    {
        try { if (parameter.Size < size) parameter.Size = size; }
        catch (NotSupportedException) { }
    }
}
