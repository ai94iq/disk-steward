namespace DiskSteward.Data;

public sealed class SqliteConnectionFactory(DataOptions options)
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = options.DatabasePath,
        ForeignKeys = true,
        Pooling = true,
    }.ToString();

    // One connection per unit of work; pooling makes this cheap. A file that is not a database
    // fails here; the connection is closed then, so the file is not left locked.
    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            connection.Open();
            // Per-connection settings; cache_size negative = KiB (~20 MB).
            connection.Execute("PRAGMA synchronous = NORMAL; PRAGMA busy_timeout = 5000; PRAGMA cache_size = -20000;");
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
