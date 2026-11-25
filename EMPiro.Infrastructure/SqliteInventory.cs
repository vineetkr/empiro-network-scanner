using System.Text.Json;
using Microsoft.Data.Sqlite;
using Empiro.Core;
using Empiro.Domain;
using Empiro.Inventory;
using Empiro.Monitoring;
namespace Empiro.Infrastructure;
public sealed class SqliteInventory(string path, CorrelationEngine correlation, ChangeDetection changes) : IInventoryStore
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
        try { await db.OpenAsync(ct); return db; }
        catch { await db.DisposeAsync(); throw; }
    }
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var db = await OpenAsync(ct); using var cmd = db.CreateCommand();
        cmd.CommandText = "PRAGMA user_version";
        var version = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        if (version > 1) throw new InvalidOperationException("This inventory was created by a newer Empiro version. Install the newer version to open it.");
        cmd.CommandText = "PRAGMA quick_check";
        if (!string.Equals(await cmd.ExecuteScalarAsync(ct) as string, "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Inventory integrity check failed. Your database was preserved; restore a backup before continuing.");
        cmd.CommandText = "PRAGMA journal_mode=WAL; CREATE TABLE IF NOT EXISTS assets(ip TEXT PRIMARY KEY, json TEXT NOT NULL); CREATE TABLE IF NOT EXISTS sessions(id TEXT PRIMARY KEY,json TEXT NOT NULL); CREATE TABLE IF NOT EXISTS events(id INTEGER PRIMARY KEY AUTOINCREMENT,json TEXT NOT NULL); PRAGMA user_version=1;";
        await cmd.ExecuteNonQueryAsync(ct);
        // An interrupted application cannot have a completed scan. Preserve its journal entry.
        cmd.CommandText = "UPDATE sessions SET json=json_set(json, '$.Outcome', 'Interrupted') WHERE json_extract(json, '$.Outcome')='Running'";
        await cmd.ExecuteNonQueryAsync(ct);
    }
    public async Task<IReadOnlyList<Asset>> LoadAsync(CancellationToken ct = default) => await ReadAsync<Asset>("assets", ct);
    public async Task<IReadOnlyList<ScanSession>> SessionsAsync(CancellationToken ct = default) => await ReadAsync<ScanSession>("sessions", ct);
    public async Task<IReadOnlyList<NetworkEvent>> EventsAsync(CancellationToken ct = default) => await ReadAsync<NetworkEvent>("events", ct);
    private async Task<IReadOnlyList<T>> ReadAsync<T>(string table, CancellationToken ct)
    {
        await using var db = await OpenAsync(ct); using var cmd = db.CreateCommand();
        cmd.CommandText = table == "events" ? "SELECT json FROM events ORDER BY id DESC LIMIT 2000" : $"SELECT json FROM {table}";
        var result = new List<T>(); await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) if (JsonSerializer.Deserialize<T>(reader.GetString(0)) is { } value) result.Add(value);
        return result;
    }
    public async Task<IReadOnlyList<NetworkEvent>> SaveAsync(Asset asset, CancellationToken ct = default)
    {
        if (asset.Synthetic) throw new InvalidOperationException("Demo data cannot enter the live inventory.");
        await gate.WaitAsync(ct);
        try
        {
            await using var db = await OpenAsync(ct); using var tx = db.BeginTransaction();
            using var read = db.CreateCommand(); read.Transaction = tx; read.CommandText = "SELECT json FROM assets WHERE ip=$ip"; read.Parameters.AddWithValue("$ip", asset.Ip);
            var old = await read.ExecuteScalarAsync(ct) as string;
            var previous = old == null ? null : JsonSerializer.Deserialize<Asset>(old);
            var merged = previous == null ? asset : correlation.Merge(previous, asset);
            var events = changes.Compare(previous, merged);
            using var save = db.CreateCommand(); save.Transaction = tx; save.CommandText = "INSERT INTO assets(ip,json) VALUES($ip,$json) ON CONFLICT(ip) DO UPDATE SET json=excluded.json";
            save.Parameters.AddWithValue("$ip", asset.Ip); save.Parameters.AddWithValue("$json", JsonSerializer.Serialize(merged)); await save.ExecuteNonQueryAsync(ct);
            foreach (var entry in events)
            {
                using var e = db.CreateCommand(); e.Transaction = tx; e.CommandText = "INSERT INTO events(json) VALUES($json)";
                e.Parameters.AddWithValue("$json", JsonSerializer.Serialize(entry)); await e.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct); return events;
        }
        finally { gate.Release(); }
    }
    public async Task SaveSessionAsync(ScanSession session, CancellationToken ct = default)
    {
        await using var db = await OpenAsync(ct); using var cmd = db.CreateCommand(); cmd.CommandText = "INSERT OR REPLACE INTO sessions(id,json) VALUES($id,$json)";
        cmd.Parameters.AddWithValue("$id", session.Id.ToString()); cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(session)); await cmd.ExecuteNonQueryAsync(ct);
    }
    public async Task UpdateMetadataAsync(string ip, bool favorite, string notes, string tags, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            await using var db = await OpenAsync(ct); using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT json FROM assets WHERE ip=$ip"; cmd.Parameters.AddWithValue("$ip", ip);
            var json = await cmd.ExecuteScalarAsync(ct) as string ?? throw new KeyNotFoundException("Device no longer exists.");
            var asset = JsonSerializer.Deserialize<Asset>(json)! with { Favorite = favorite, Notes = notes, Tags = tags };
            cmd.CommandText = "UPDATE assets SET json=$json WHERE ip=$ip"; cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(asset)); await cmd.ExecuteNonQueryAsync(ct);
        }
        finally { gate.Release(); }
    }
}
