using System.Data.Common;
using DynamicDataCore.Abstractions.Procedures;

namespace DynamicDataCore.Infrastructure.Procedures;

internal sealed class ProcedureMultipleResult : IProcedureMultipleResult
{
    private readonly DbConnection _connection;
    private readonly DbCommand _command;
    private readonly DbDataReader _reader;
    private bool _hasCurrentSet = true;

    internal ProcedureMultipleResult(DbConnection connection, DbCommand command, DbDataReader reader)
    {
        _connection = connection;
        _command = command;
        _reader = reader;
    }

    public async Task<IReadOnlyList<T>> ReadAsync<T>(CancellationToken cancellationToken = default)
    {
        if (!_hasCurrentSet)
            throw new InvalidOperationException("There are no more result sets to read.");

        var rows = await SqlProcedureExecutor.ReadSetAsync<T>(_reader, cancellationToken);
        _hasCurrentSet = await _reader.NextResultAsync(cancellationToken);
        return rows;
    }

    public async Task<T?> ReadSingleOrDefaultAsync<T>(CancellationToken cancellationToken = default)
        => SqlProcedureExecutor.SingleOrDefault(await ReadAsync<T>(cancellationToken));

    public async ValueTask DisposeAsync()
    {
        await _reader.DisposeAsync();
        await _command.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
