using System.Data;
using System.Data.Common;
using DynamicDataCore.Abstractions.Procedures;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Server;

namespace DynamicDataCore.Infrastructure.Procedures;

/// <summary>
/// SQL Server implementation of <see cref="ISqlProcedureExecutor"/> on Microsoft.Data.SqlClient.
/// Every call uses <see cref="CommandType.StoredProcedure"/>, a fresh connection, and parameters only.
/// This is the single place in the libraries allowed to assign <c>CommandText</c>.
/// </summary>
public sealed class SqlProcedureExecutor : ISqlProcedureExecutor
{
    private readonly string _connectionString;

    /// <summary>Creates an executor. The connection string is held in memory only and never logged.</summary>
    public SqlProcedureExecutor(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> QueryAsync<T>(ProcedureName procedure, ProcedureParameters? parameters = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(procedure);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, procedure, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await ReadSetAsync<T>(reader, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T?> QuerySingleOrDefaultAsync<T>(ProcedureName procedure, ProcedureParameters? parameters = null, CancellationToken cancellationToken = default)
    {
        var rows = await QueryAsync<T>(procedure, parameters, cancellationToken);
        return SingleOrDefault(rows);
    }

    /// <inheritdoc />
    public async Task<ProcedureResult> ExecuteAsync(ProcedureName procedure, ProcedureParameters? parameters = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(procedure);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, procedure, parameters);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);

        var outputs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        int? returnValue = null;
        foreach (var p in parameters?.Items ?? [])
        {
            var value = command.Parameters[p.Name].Value;
            switch (p.Direction)
            {
                case ProcedureParameterDirection.ReturnValue:
                    returnValue = value is DBNull or null ? null : Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case ProcedureParameterDirection.Output:
                case ProcedureParameterDirection.InputOutput:
                    outputs[p.Name] = value is DBNull ? null : value;
                    break;
            }
        }

        return new ProcedureResult { RowsAffected = affected, ReturnValue = returnValue, OutputValues = outputs };
    }

    /// <inheritdoc />
    public async Task<IProcedureMultipleResult> QueryMultipleAsync(ProcedureName procedure, ProcedureParameters? parameters = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(procedure);
        var connection = await OpenAsync(cancellationToken);
        SqlCommand? command = null;
        try
        {
            command = CreateCommand(connection, procedure, parameters);
            var reader = await command.ExecuteReaderAsync(cancellationToken);
            return new ProcedureMultipleResult(connection, command, reader);
        }
        catch
        {
            if (command is not null) await command.DisposeAsync();
            await connection.DisposeAsync();
            throw;
        }
    }

    internal static T? SingleOrDefault<T>(IReadOnlyList<T> rows) => rows.Count switch
    {
        0 => default,
        1 => rows[0],
        _ => throw new InvalidOperationException("The procedure returned more than one row where at most one was expected.")
    };

    internal static async Task<IReadOnlyList<T>> ReadSetAsync<T>(DbDataReader reader, CancellationToken cancellationToken)
    {
        var rows = new List<T>();
        if (reader.FieldCount == 0) return rows;

        var map = RowMapper.CreateFactory<T>(reader);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(map(reader));
        return rows;
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static SqlCommand CreateCommand(SqlConnection connection, ProcedureName procedure, ProcedureParameters? parameters)
    {
        var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;

        // RS0030 is suppressed ONLY here: CommandText receives a ProcedureName that was validated as
        // 'schema.name' ([A-Za-z0-9_] parts), and CommandType.StoredProcedure makes the driver treat it as an
        // RPC call by name rather than as batch text. No other code in the libraries may assign CommandText.
#pragma warning disable RS0030
        command.CommandText = procedure.FullName;
#pragma warning restore RS0030

        foreach (var p in parameters?.Items ?? [])
            command.Parameters.Add(ToSqlParameter(p));
        return command;
    }

    private static SqlParameter ToSqlParameter(ProcedureParameter p)
    {
        var parameter = new SqlParameter
        {
            ParameterName = p.Name,
            SqlDbType = p.DbType,
            Direction = p.Direction switch
            {
                ProcedureParameterDirection.Output => ParameterDirection.Output,
                ProcedureParameterDirection.InputOutput => ParameterDirection.InputOutput,
                ProcedureParameterDirection.ReturnValue => ParameterDirection.ReturnValue,
                _ => ParameterDirection.Input
            }
        };

        if (p.Size is { } size) parameter.Size = size;
        if (p.Precision is { } precision) parameter.Precision = precision;
        if (p.Scale is { } scale) parameter.Scale = scale;

        if (p.DbType == SqlDbType.Structured)
        {
            parameter.TypeName = p.TableType!.FullName;
            parameter.Value = p.Value switch
            {
                DataTable table => table,
                TableValuedRows rows => ToRecords(rows),
                _ => throw new InvalidOperationException("Unsupported table-valued parameter value.")
            };
        }
        else if (p.Direction is ProcedureParameterDirection.Input or ProcedureParameterDirection.InputOutput)
        {
            parameter.Value = p.Value ?? DBNull.Value;
        }

        return parameter;
    }

    // The driver rejects an empty SqlDataRecord enumeration and DBNull for a TVP; an unset (null) Value is
    // sent as an empty READONLY table, so an empty row set maps to null.
    private static object? ToRecords(TableValuedRows rows)
    {
        var metadata = rows.Columns.Select(ToMetaData).ToArray();
        var records = new List<SqlDataRecord>();
        foreach (var row in rows.Rows)
        {
            if (row.Length != metadata.Length)
                throw new ArgumentException($"A TVP row has {row.Length} values but {metadata.Length} columns were declared.");

            var record = new SqlDataRecord(metadata);
            record.SetValues(row.Select(v => v ?? DBNull.Value).ToArray());
            records.Add(record);
        }

        return records.Count == 0 ? null : records;
    }

    private static SqlMetaData ToMetaData(TableValueColumn c) => c.DbType switch
    {
        SqlDbType.NVarChar or SqlDbType.VarChar or SqlDbType.NChar or SqlDbType.Char
            or SqlDbType.VarBinary or SqlDbType.Binary => new SqlMetaData(c.Name, c.DbType, c.Size ?? -1),
        SqlDbType.Decimal => new SqlMetaData(c.Name, c.DbType, c.Precision ?? 18, c.Scale ?? 0),
        _ => new SqlMetaData(c.Name, c.DbType)
    };
}
