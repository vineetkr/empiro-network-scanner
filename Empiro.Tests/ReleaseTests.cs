using Empiro.Domain;
using Empiro.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;
namespace Empiro.Tests;
public sealed class ReleaseTests
{
    [Fact] public async Task PartialObservationDoesNotInventHostnameChange()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        try
        {
            var store = new SqliteInventory(path, new(), new()); await store.InitializeAsync();
            await store.SaveAsync(new Asset { Ip = "192.0.2.1", Hostname = "Known host", Mac = "AA" });
            var events = await store.SaveAsync(new Asset { Ip = "192.0.2.1", Mac = "AA" });
            Assert.Empty(events); Assert.Equal("Known host", Assert.Single(await store.LoadAsync()).Hostname);
        }
        finally { SqliteConnection.ClearAllPools(); File.Delete(path); }
    }
    [Fact] public async Task StartupMarksInterruptedScanAndPreservesInventory()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        try
        {
            var store = new SqliteInventory(path, new(), new()); await store.InitializeAsync();
            await store.SaveAsync(new Asset { Ip = "192.0.2.1", Hostname = "Preserved" });
            await store.SaveSessionAsync(new(Guid.NewGuid(), DateTimeOffset.UtcNow, "192.0.2.0/24", "Standard", 0, "Running", 0));
            await store.InitializeAsync();
            Assert.Equal("Interrupted", Assert.Single(await store.SessionsAsync()).Outcome);
            Assert.Equal("Preserved", Assert.Single(await store.LoadAsync()).Hostname);
        }
        finally { SqliteConnection.ClearAllPools(); File.Delete(path); }
    }
    [Fact] public async Task FutureSchemaIsNotOpenedOrRewritten()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        try
        {
            await using (var db = new SqliteConnection($"Data Source={path}"))
            { await db.OpenAsync(); using var cmd = db.CreateCommand(); cmd.CommandText = "PRAGMA user_version=99"; await cmd.ExecuteNonQueryAsync(); }
            var store = new SqliteInventory(path, new(), new());
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.InitializeAsync());
        }
        finally { SqliteConnection.ClearAllPools(); File.Delete(path); }
    }
    [Fact] public async Task InvalidSettingsCannotBeSaved()
    { await Assert.ThrowsAsync<ArgumentException>(() => new SettingsStore(Path.GetTempPath()).SaveAsync(new(Concurrency: 0))); }
}
