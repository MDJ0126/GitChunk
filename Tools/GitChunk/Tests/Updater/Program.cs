using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

// In-process HTTP fixture: no release download or real user-cache mutation.
var fixture = Path.GetFullPath(args[0]); Directory.CreateDirectory(fixture);
Environment.SetEnvironmentVariable("GITCHUNK_SKIP_UPDATE_CHECK", "1");
var exe = Path.GetFullPath(args[1]);
byte[] archive;
using (var memory = new MemoryStream()) {
    using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true)) {
        using var entry = zip.CreateEntry(OperatingSystem.IsWindows() ? "GitChunk.exe" : OperatingSystem.IsLinux() ? "git-chunk" : "GitChunk.app/Contents/MacOS/GitChunk-cli").Open();
        entry.Write(File.ReadAllBytes(exe));
    }
    archive = memory.ToArray();
}
foreach (var scenario in new[] { "corrupt", "missing", "install-failure", "success" }) {
    var folder = Path.Combine(fixture, scenario); Directory.CreateDirectory(folder);
    var cache = Path.Combine(folder, "cache");
    Environment.SetEnvironmentVariable("GITCHUNK_UPDATE_CACHE", cache);
    var root = Path.Combine(folder, "repo"); Directory.CreateDirectory(root);
    if (scenario != "install-failure") {
        using var git = Process.Start(new ProcessStartInfo("git") { ArgumentList = { "-C", root, "init" }, UseShellExecute = false })!;
        git.WaitForExit(); if (git.ExitCode != 0) throw new Exception("git init failed");
    }
    using var client = new HttpClient(new Fixture(archive, scenario));
    var failed = false;
    try { await ToolUpdate.UpdateAsync(root, client); }
    catch (Exception) { failed = true; }
    if (scenario == "success") {
        if (failed || !File.Exists(Path.Combine(cache, "active.json")) || !File.Exists(Path.Combine(root, ".gitchunk/chunks.txt"))) throw new Exception("Update did not activate/install.");
        if (ToolUpdate.Forward(new[] { "version" }) != 0) throw new Exception("Old command did not forward to new version.");
        var active = JsonDocument.Parse(File.ReadAllText(Path.Combine(cache, "active.json")));
        var path = Path.Combine(cache, active.RootElement.GetProperty("Executable").GetString()!);
        File.AppendAllText(path, "tampered");
        failed = false;
        try { ToolUpdate.Forward(new[] { "version" }); } catch { failed = true; }
        if (!failed) throw new Exception("Corrupt cached executable was launched.");
        if (ToolUpdate.Forward(new[] { "update" }).HasValue) throw new Exception("Corrupt cache prevents updater repair.");
    } else if (!failed || File.Exists(Path.Combine(cache, "active.json"))) throw new Exception("Failed update activated a command.");
}
Console.WriteLine("PASS: verified download, checksum/missing asset rejection, failed install retains command, activation, old command forwarding, cache integrity");

class Fixture(byte[] bytes, string scenario) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) {
        var url = request.RequestUri!.AbsoluteUri;
        var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "arm64" : "x64";
        var name = OperatingSystem.IsWindows() ? "GitChunk-Windows-x64.zip" : $"GitChunk-{(OperatingSystem.IsLinux() ? "linux" : "osx")}-{arch}.zip";
        var baseUrl = "https://github.com/MDJ0126/GitChunk/releases/download/v1.3.0/";
        byte[] content;
        if (url.EndsWith("/latest")) content = JsonSerializer.SerializeToUtf8Bytes(new { tag_name = "v1.3.0", assets = new[] { new { name, browser_download_url = baseUrl + name }, new { name = "SHA256SUMS", browser_download_url = baseUrl + "SHA256SUMS" } } });
        else if (url.EndsWith("SHA256SUMS")) content = System.Text.Encoding.UTF8.GetBytes((scenario == "corrupt" ? new string('0', 64) : Convert.ToHexString(SHA256.HashData(bytes))) + "  " + (scenario == "missing" ? "wrong.zip" : name) + "\n");
        else if (url == baseUrl + name) content = bytes;
        else throw new Exception("Unexpected HTTP request: " + url);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
    }
}
static class GitIntegration { internal static int Run(string[] args) => throw new Exception("Unexpected local reinstall branch."); }
