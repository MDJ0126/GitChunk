using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

static class ToolUpdate
{
    const string Releases = "https://api.github.com/repos/MDJ0126/GitChunk/releases/latest";
    static string Cache => Environment.GetEnvironmentVariable("GITCHUNK_UPDATE_CACHE") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GitChunk", "updates");
    static string Manifest => Path.Combine(Cache, "active.json");
    internal static Version Current => typeof(ToolUpdate).Assembly.GetName().Version ?? new Version(0, 0);
    internal static string VersionText => typeof(ToolUpdate).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? Current.ToString();
    record Active(string Version, string Executable, string Sha256);
    internal static int? Forward(string[] args)
    {
        try { return ForwardCore(args); }
        catch when (args.Length > 0 && args[0] == "update") {
            Console.Error.WriteLine("The cached update is unavailable or damaged. Downloading a verified replacement.");
            return null;
        }
    }
    static int? ForwardCore(string[] args)
    {
        if (!File.Exists(Manifest)) return null;
        var active = JsonSerializer.Deserialize<Active>(File.ReadAllText(Manifest)) ?? throw new Exception("Invalid update manifest.");
        if (!Version.TryParse(active.Version, out var version) || version <= Current) return null;
        var path = Path.GetFullPath(Path.Combine(Cache, active.Executable));
        if (!path.StartsWith(Path.GetFullPath(Cache) + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) throw new Exception("Invalid update executable path.");
        using (var file = File.OpenRead(path))
            if (Convert.ToHexString(SHA256.HashData(file)) != active.Sha256) throw new Exception("Updated executable checksum mismatch. Run git chunk update to repair it.");
        var start = new ProcessStartInfo(path) { UseShellExecute = false };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var child = Process.Start(start) ?? throw new Exception("Could not start updated Git Chunk.");
        child.WaitForExit(); return child.ExitCode;
    }
    static HttpClient Client(int seconds)
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(seconds) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GitChunk/" + Current);
        return client;
    }
    static async Task<JsonDocument> Latest(HttpClient client) => JsonDocument.Parse(await client.GetStringAsync(Releases));
    static Version ReleaseVersion(JsonElement release)
    {
        var tag = release.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v'), out var version)) throw new Exception("Invalid release version.");
        return version;
    }
    internal static void Check()
    {
        if (Environment.GetEnvironmentVariable("GITCHUNK_SKIP_UPDATE_CHECK") == "1") return;
        try
        {
            using var client = Client(5); using var release = Latest(client).GetAwaiter().GetResult();
            var latest = ReleaseVersion(release.RootElement);
            if (latest > Current) Console.Error.WriteLine($"Git Chunk {latest} is available (running {VersionText}). Run git chunk update to update the program and this repository.");
        }
        catch { Console.Error.WriteLine("Could not check for updates. Installed the current version; retry with git chunk update when online."); }
    }
    internal static int Update(string root)
    {
        using var client = Client(180);
        return UpdateAsync(root, client).GetAwaiter().GetResult();
    }
    static string Asset(JsonElement release, string name)
    {
        var match = release.GetProperty("assets").EnumerateArray().SingleOrDefault(x => x.GetProperty("name").GetString() == name);
        if (match.ValueKind == JsonValueKind.Undefined) throw new Exception("Release asset missing: " + name);
        var url = match.GetProperty("browser_download_url").GetString() ?? "";
        if (!url.StartsWith("https://github.com/MDJ0126/GitChunk/releases/download/", StringComparison.Ordinal)) throw new Exception("Unexpected release asset URL.");
        return url;
    }
    internal static async Task<int> UpdateAsync(string root, HttpClient client)
    {
        using var release = await Latest(client);
        var version = ReleaseVersion(release.RootElement);
        if (version <= Current)
        {
            Console.WriteLine($"Git Chunk {VersionText}: no newer release. Applying this version to the repository.");
            return GitIntegration.Run(new[] { "git-install", root });
        }
        var windows = OperatingSystem.IsWindows();
        if (!windows && !OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) throw new Exception("Unsupported operating system.");
        var arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        var name = windows ? "GitChunk-Windows-x64.zip" : $"GitChunk-{(OperatingSystem.IsLinux() ? "linux" : "osx")}-{arch}.zip";
        var sums = await client.GetStringAsync(Asset(release.RootElement, "SHA256SUMS"));
        var expected = sums.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).SingleOrDefault(x => x.Length == 2 && x[1].TrimStart('*') == name);
        if (expected == null || expected[0].Length != 64 || !expected[0].All(Uri.IsHexDigit)) throw new Exception("Release checksum missing: " + name);
        Console.WriteLine($"Downloading Git Chunk {version}…");
        var bytes = await client.GetByteArrayAsync(Asset(release.RootElement, name));
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(expected[0], StringComparison.OrdinalIgnoreCase)) throw new Exception("Release checksum mismatch. Nothing was installed.");
        Directory.CreateDirectory(Cache);
        var folder = Path.Combine(Cache, version + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        var entryName = windows ? "git-chunk.exe" : OperatingSystem.IsLinux() ? "git-chunk" : "GitChunk.app/Contents/MacOS/GitChunk-cli";
        var entry = zip.GetEntry(entryName) ?? (windows ? zip.GetEntry("GitChunk.exe") : null) ?? throw new Exception("Executable missing from release.");
        var executable = Path.Combine(folder, windows ? "git-chunk.exe" : "git-chunk");
        entry.ExtractToFile(executable);
        if (!windows) File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var probe = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true };
        probe.ArgumentList.Add("version");
        using (var process = Process.Start(probe) ?? throw new Exception("Could not verify release version.")) {
            var output = await process.StandardOutput.ReadToEndAsync(); await process.WaitForExitAsync();
            var declared = output.Split('\n')[0].Trim().Replace("Git Chunk ", "").Split('+')[0];
            if (process.ExitCode != 0 || !Version.TryParse(declared, out var actual) || actual != version) throw new Exception("Release executable version does not match its tag. Nothing was installed.");
        }
        var start = new ProcessStartInfo(executable) { UseShellExecute = false };
        start.ArgumentList.Add("git-install"); start.ArgumentList.Add(root);
        using var child = Process.Start(start) ?? throw new Exception("Could not apply update.");
        await child.WaitForExitAsync();
        if (child.ExitCode != 0) throw new Exception("Repository update failed. The command remains on the previous version.");
        using var file = File.OpenRead(executable);
        var active = new Active(version.ToString(), Path.GetRelativePath(Cache, executable), Convert.ToHexString(SHA256.HashData(file)));
        var temporary = Manifest + ".tmp-" + Guid.NewGuid().ToString("N");
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(active)); File.Move(temporary, Manifest, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        Console.WriteLine($"Updated Git Chunk to {version}. Future git chunk commands use this version. Run git chunk install in other repositories to apply it there.");
        return 0;
    }
}
