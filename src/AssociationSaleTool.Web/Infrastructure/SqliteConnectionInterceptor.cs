using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace AssociationSaleTool.Web.Infrastructure;

public sealed class SqliteConnectionInterceptor : DbConnectionInterceptor
{
    public override InterceptionResult ConnectionOpening(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result)
    {
        ConfigureConnection(connection);
        return base.ConnectionOpening(connection, eventData, result);
    }

    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        ConfigureConnection(connection);
        return base.ConnectionOpeningAsync(connection, eventData, result, cancellationToken);
    }

    private static void ConfigureConnection(DbConnection connection)
    {
        if (connection is not SqliteConnection sqliteConnection)
        {
            return;
        }

        if (sqliteConnection.State != System.Data.ConnectionState.Open)
        {
            sqliteConnection.Open();
        }

        using var pragmaCommand = sqliteConnection.CreateCommand();
        pragmaCommand.CommandText = """
            PRAGMA foreign_keys = ON;
            PRAGMA journal_mode = WAL;
            PRAGMA busy_timeout = 5000;
            """;
        pragmaCommand.ExecuteNonQuery();
    }
}
