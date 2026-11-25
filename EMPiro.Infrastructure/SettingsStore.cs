using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.Versioning;
namespace Empiro.Infrastructure;
public sealed record UserSettings(bool LightTheme = false, int Concurrency = 32, int TimeoutMs = 700, int MaximumHosts = 4096);
public sealed class SettingsStore(string directory)
{
    public UserSettings Load()
    {
        try { var settings = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(Path.Combine(directory, "settings.json"))) ?? new(); return IsValid(settings) ? settings : new(); }
        catch (Exception ex) when (ex is IOException or JsonException) { return new(); }
    }
    public async Task SaveAsync(UserSettings settings)
    {
        if (!IsValid(settings)) throw new ArgumentException("Scan preferences are outside supported limits.");
        Directory.CreateDirectory(directory); var file = Path.Combine(directory, "settings.json");
        await File.WriteAllTextAsync(file + ".tmp", JsonSerializer.Serialize(settings)); File.Move(file + ".tmp", file, true);
    }
    private static bool IsValid(UserSettings s) => s.Concurrency is >= 1 and <= 256 && s.TimeoutMs is >= 100 and <= 10000 && s.MaximumHosts is >= 1 and <= 65536;
}
/// <summary>Secrets are protected for the current Windows account using DPAPI.</summary>
[SupportedOSPlatform("windows")]
public sealed class CredentialVault(string directory)
{
    public void Save(Guid reference, string secret)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, reference + ".secret"), ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), null, DataProtectionScope.CurrentUser));
    }
    public string Load(Guid reference) => Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(Path.Combine(directory, reference + ".secret")), null, DataProtectionScope.CurrentUser));
}
