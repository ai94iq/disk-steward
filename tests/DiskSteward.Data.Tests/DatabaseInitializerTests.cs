namespace DiskSteward.Data.Tests;

public sealed class DatabaseInitializerTests
{
    [Fact]
    public void Migrations_are_applied()
    {
        using var db = new TempDatabase();
        using var connection = db.Factory.Open();
        Assert.True(connection.ExecuteScalar<long>("PRAGMA user_version;") >= 1);
    }

    [Fact]
    public void Second_start_writes_a_daily_backup()
    {
        using var db = new TempDatabase();
        db.Initializer().BackupAndMigrate();
        Assert.Single(Directory.GetFiles(db.Options.BackupDirectory, "data-*-daily.db"));
    }

    [Fact]
    public void A_corrupt_database_file_fails_startup_and_is_left_as_it_was()
    {
        // App.OnStartup shows the startup error for this; the file stays for the user to recover.
        var directory = Directory.CreateTempSubdirectory("DiskSteward-tests-").FullName;
        try
        {
            var options = new DataOptions(Path.Combine(directory, "test.db"), Path.Combine(directory, "backups"));
            byte[] garbage = [.. Enumerable.Repeat((byte)0x5A, 4096)];
            File.WriteAllBytes(options.DatabasePath, garbage);
            var initializer = new DatabaseInitializer(new SqliteConnectionFactory(options), options, NullLogger<DatabaseInitializer>.Instance);

            Assert.Throws<SqliteException>(initializer.BackupAndMigrate);
            SqliteConnection.ClearAllPools();
            Assert.Equal(garbage, File.ReadAllBytes(options.DatabasePath));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }
}
