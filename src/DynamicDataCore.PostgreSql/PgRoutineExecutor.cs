using System.Text.Json;
using DynamicDataCore.Abstractions;
using DynamicDataCore.Abstractions.Procedures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace DynamicDataCore.PostgreSql;

/// <summary>
/// Npgsql implementation of <see cref="IPgRoutineExecutor"/>. It borrows the connection and the current
/// transaction of the scoped <see cref="IAppDbContext"/> (EF Core opens and ref-counts the connection), so a
/// routine call made inside <c>IUnitOfWork.BeginTransactionAsync</c> is part of that transaction.
/// This is the single place in the PostgreSQL package allowed to assign <c>CommandText</c>.
/// </summary>
public sealed class PgRoutineExecutor : IPgRoutineExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAppDbContext _context;

    /// <summary>Creates an executor over the scoped context.</summary>
    public PgRoutineExecutor(IAppDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> QueryAsync<T>(ProcedureName routine, PgParameters? parameters = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(routine);
        await using var lease = await LeaseAsync(cancellationToken);
        await using var command = CreateCommand(lease, RoutineKind.SelectRows, routine, parameters);
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var rows = new List<T>();
            if (reader.FieldCount == 0) return rows;

            var map = RowMapper.CreateFactory<T>(reader);
            while (await reader.ReadAsync(cancellationToken))
                rows.Add(map(reader));
            return rows;
        }
        catch (PostgresException ex)
        {
            throw new PgRoutineException(ex);
        }
    }

    /// <inheritdoc />
    public async Task<T?> QuerySingleOrDefaultAsync<T>(ProcedureName routine, PgParameters? parameters = null, CancellationToken cancellationToken = default)
    {
        var rows = await QueryAsync<T>(routine, parameters, cancellationToken);
        return rows.Count switch
        {
            0 => default,
            1 => rows[0],
            _ => throw new InvalidOperationException("The routine returned more than one row where at most one was expected.")
        };
    }

    /// <inheritdoc />
    public async Task<T?> JsonAsync<T>(ProcedureName routine, PgParameters? parameters = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(routine);
        await using var lease = await LeaseAsync(cancellationToken);
        await using var command = CreateCommand(lease, RoutineKind.SelectRows, routine, parameters);
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || await reader.IsDBNullAsync(0, cancellationToken))
                return default;

            var json = await reader.GetFieldValueAsync<string>(0, cancellationToken);
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (PostgresException ex)
        {
            throw new PgRoutineException(ex);
        }
    }

    /// <inheritdoc />
    public async Task ExecuteFunctionAsync(ProcedureName routine, PgParameters? parameters = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(routine);
        await using var lease = await LeaseAsync(cancellationToken);
        await using var command = CreateCommand(lease, RoutineKind.SelectScalar, routine, parameters);
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException ex)
        {
            throw new PgRoutineException(ex);
        }
    }

    /// <inheritdoc />
    public async Task CallAsync(ProcedureName procedure, PgParameters? parameters = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(procedure);
        await using var lease = await LeaseAsync(cancellationToken);
        await using var command = CreateCommand(lease, RoutineKind.Call, procedure, parameters);
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException ex)
        {
            throw new PgRoutineException(ex);
        }
    }

    internal enum RoutineKind
    {
        SelectRows,
        SelectScalar,
        Call
    }

    /// <summary>
    /// Builds the statement. The routine name was validated as <c>schema.name</c> (word characters only) and each
    /// parameter name matches <c>[A-Za-z_][A-Za-z0-9_]{0,62}</c>, so the text contains no caller-controlled data.
    /// Arguments use PostgreSQL named notation (<c>arg =&gt; @arg</c>).
    /// </summary>
    internal static string BuildText(RoutineKind kind, ProcedureName routine, PgParameters? parameters)
    {
        var args = string.Join(", ", (parameters?.Items ?? []).Select(p => $"{p.Name} => @{p.Name}"));
        return kind switch
        {
            RoutineKind.SelectRows => $"SELECT * FROM {routine.FullName}({args})",
            RoutineKind.SelectScalar => $"SELECT {routine.FullName}({args})",
            RoutineKind.Call => $"CALL {routine.FullName}({args})",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static NpgsqlCommand CreateCommand(Lease lease, RoutineKind kind, ProcedureName routine, PgParameters? parameters)
    {
        var command = lease.Connection.CreateCommand();
        command.Transaction = lease.Transaction;

        // RS0030 is suppressed ONLY here: the text comes from BuildText, which only concatenates a validated
        // 'schema.name' and validated parameter names; every value is sent as an NpgsqlParameter.
#pragma warning disable RS0030
        command.CommandText = BuildText(kind, routine, parameters);
#pragma warning restore RS0030

        foreach (var p in parameters?.Items ?? [])
            command.Parameters.Add(new NpgsqlParameter(p.Name, p.DbType) { Value = p.Value ?? DBNull.Value });
        return command;
    }

    private async Task<Lease> LeaseAsync(CancellationToken cancellationToken)
    {
        var database = _context.Database;
        await database.OpenConnectionAsync(cancellationToken);
        try
        {
            if (database.GetDbConnection() is not NpgsqlConnection connection)
                throw new InvalidOperationException("The DbContext is not using Npgsql; PgRoutineExecutor requires PostgreSQL.");

            NpgsqlTransaction? transaction = null;
            if (database.CurrentTransaction is { } current)
            {
                transaction = current.GetDbTransaction() as NpgsqlTransaction
                    ?? throw new InvalidOperationException("The current transaction is not an Npgsql transaction.");
            }

            return new Lease(database, connection, transaction);
        }
        catch
        {
            await database.CloseConnectionAsync();
            throw;
        }
    }

    private sealed class Lease(DatabaseFacade database, NpgsqlConnection connection, NpgsqlTransaction? transaction) : IAsyncDisposable
    {
        public NpgsqlConnection Connection { get; } = connection;

        public NpgsqlTransaction? Transaction { get; } = transaction;

        public async ValueTask DisposeAsync() => await database.CloseConnectionAsync();
    }
}
