using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

static partial class GitIntegration
{
    static string WithoutChunkAttributes(string text)
    {
        var result = new List<string>();
        bool managed = false;
        var generated = new HashSet<string> {
            "*.meta -filter", ".gitattributes -filter", ".gitignore -filter",
            ".gitchunk/** -filter -text -diff -merge", ".resourcesplitter/** -filter -text -diff -merge",
            ".chunkignore -filter text eol=lf diff", ".gitchunk/rules.txt -filter text eol=lf diff",
            ".gitchunk/chunks.txt -filter text eol=lf diff", ".gitchunk/presets/** -filter text eol=lf diff",
            "GitChunk.* -filter", "ResourceSplitter.* -filter", "Tools/ResourceSplitter/** -filter"
        };
        foreach (var line in text.Replace("\r\n", "\n").Split('\n')) {
            if (line is "# GitChunk automatic storage" or "# ResourceSplitter automatic storage") { managed = true; continue; }
            if (line == "# GitChunk converted LFS files") { managed = false; continue; }
            if (managed && generated.Contains(line)) continue;
            if (line.TrimStart().StartsWith('#')) { result.Add(line); continue; }
            if (Regex.IsMatch(line, @"\sfilter=(?:gitchunk|resourcesplitter)(?:\s|$)")) {
                var stripped = Regex.Replace(line, @"\sfilter=(?:gitchunk|resourcesplitter)(?=\s|$)", "");
                // Drop lines consisting solely of the generated binary attributes.
                if (Regex.IsMatch(stripped, @"^(?:""[^""]+""|\S+)(?:\s+(?:-text|-diff|-merge))*\s*$")) continue;
                result.Add(stripped);
            } else result.Add(line);
        }
        return string.Join("\n", result);
    }
    static void MoveUninstallStorage(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source)) File.Move(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (var folder in Directory.EnumerateDirectories(source)) MoveUninstallStorage(folder, Path.Combine(destination, Path.GetFileName(folder)));
        Directory.Delete(source); // Only empty directories are removed.
    }
    static void Uninstall(string root)
    {
        Directory.SetCurrentDirectory(Path.GetFullPath(root));
        var actual = Path.GetFullPath(Text("rev-parse", "--show-toplevel"));
        if (!actual.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)), FileSystemPaths.Comparison))
            throw new Exception("Specify the Git repository root.");
        if (Text("--no-optional-locks", "status", "--porcelain", "--untracked-files=no").Length != 0)
            throw new Exception("Uninstall requires a clean tracked working tree and index. Commit or stash changes first.");
        var entries = IndexedAssets();
        var pointers = Pointers(entries.Where(e => e.Size <= 1024 * 1024).Select(e => e.Oid));
        var assets = entries.Where(e => pointers.ContainsKey(e.Oid)).ToList();
        foreach (var asset in assets) {
            if (pointers[asset.Oid].Size >= 100L * 1024 * 1024)
                throw new Exception("Cannot uninstall: " + asset.Path + " is 100 MiB or more. Move it to another storage method first. No configuration was removed.");
        }
        var attributes = entries.Where(e => e.Path == ".gitattributes" || e.Path.EndsWith("/.gitattributes", StringComparison.Ordinal))
            .Select(e => SafePath(actual, e.Path)).ToList();
        var rootAttributes = SafePath(actual, ".gitattributes");
        if (!attributes.Contains(rootAttributes)) attributes.Add(rootAttributes);
        var infoAttributes = Path.GetFullPath(Text("rev-parse", "--git-path", "info/attributes"));
        if (File.Exists(infoAttributes)) attributes.Add(infoAttributes);
        var hooks = Path.GetFullPath(Text("rev-parse", "--git-path", "hooks"));
        var runtime = RuntimeTool();
        var managedHooks = new List<string>();
        foreach (var (name, command) in new[] { ("pre-commit", "git-prepare"), ("pre-push", "git-check-push") }) {
            var path = Path.Combine(hooks, name);
            if (!File.Exists(path)) continue;
            var text = File.ReadAllText(path).Replace("\r\n", "\n");
            if (text == "#!/bin/sh\n# GitChunk managed hook\nexec " + Quote(runtime) + " " + command + "\n") managedHooks.Add(path);
            else if (text.Contains("# GitChunk managed hook") || text.Contains("# ResourceSplitter managed hook"))
                throw new Exception("Modified managed hook must be separated before uninstall: " + path);
        }
        var ignore = SafePath(actual, ".gitignore");
        var chunkIgnore = SafePath(actual, ".chunkignore");
        var storage = new[] { SafePath(actual, ".gitchunk"), SafePath(actual, ".resourcesplitter") }.Where(Directory.Exists).ToList();
        // Check every child before moving a storage directory into the local backup.
        var pending = new Stack<string>(storage);
        while (pending.TryPop(out var folder)) foreach (var child in Directory.EnumerateFileSystemEntries(folder)) {
            SafePath(actual, Path.GetRelativePath(actual, child));
            if (Directory.Exists(child)) pending.Push(child);
        }
        var index = Path.GetFullPath(Text("rev-parse", "--git-path", "index"));
        var config = Path.GetFullPath(Text("rev-parse", "--git-path", "config"));
        var metadata = Path.GetFullPath(Text("rev-parse", "--git-path", "gitchunk/uninstall-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(metadata);
        var snapshots = attributes.Concat(managedHooks).Concat(new[] { ignore, chunkIgnore, index, config }).Distinct()
            .Select((path, i) => (Path: path, Backup: Path.Combine(metadata, "snapshot-" + i), Exists: File.Exists(path))).ToList();
        foreach (var item in snapshots) if (item.Exists) File.Copy(item.Path, item.Backup);
        var originals = new List<(string Path, string Restored, string? PointerBackup, string Mode)>();
        var moved = new List<(string Path, string Backup)>();
        try {
            // Verify every reconstruction before changing filters, index, or working files.
            foreach (var asset in assets) {
                var path = SafePath(actual, asset.Path);
                if (!File.Exists(path)) throw new Exception("Missing working file: " + asset.Path);
                var pointer = pointers[asset.Oid];
                var indexed = Git(null, "cat-file", "blob", asset.Oid);
                var isPointer = new FileInfo(path).Length == indexed.Length && File.ReadAllBytes(path).AsSpan().SequenceEqual(indexed);
                if (!isPointer) {
                    using var input = File.OpenRead(path);
                    if (input.Length != pointer.Size || Convert.ToHexString(SHA256.HashData(input)) != pointer.Sha256)
                        throw new Exception("Working original differs from the indexed version: " + asset.Path);
                }
                var restored = Path.Combine(metadata, "original-" + originals.Count);
                using (var output = File.Create(restored)) using (var input = new MemoryStream(indexed)) Smudge(input, output);
                string? pointerBackup = null;
                if (isPointer) { pointerBackup = restored + ".pointer"; File.Copy(path, pointerBackup); }
                var mode = Text("ls-files", "--stage", "--", asset.Path).Split(' ')[0];
                originals.Add((path, restored, pointerBackup, mode));
            }
            foreach (var path in attributes) if (File.Exists(path))
                File.WriteAllText(path, WithoutChunkAttributes(File.ReadAllText(path)), new UTF8Encoding(false));
            if (File.Exists(ignore)) {
                var text = File.ReadAllText(ignore).Replace("\r\n", "\n");
                text = text.Replace("# GitChunk chunk cache (required chunks are staged by the hook)\n/.gitchunk/objects/\n", "");
                File.WriteAllText(ignore, text, new UTF8Encoding(false));
            }
            foreach (var driver in new[] { "gitchunk", "resourcesplitter" })
                foreach (var (key, value) in new[] { ("clean", Quote(runtime) + " git-clean %f"), ("smudge", Quote(runtime) + " git-smudge") }) {
                    var name = "filter." + driver + "." + key;
                    string current; try { current = Text("config", "--local", "--get", name); } catch { continue; }
                    if (current == value) Text("config", "--local", "--unset-all", name);
                }
            foreach (var driver in new[] { "gitchunk", "resourcesplitter" }) {
                string settings; try { settings = Text("config", "--local", "--get-regexp", "^filter\\." + driver + "\\.(clean|smudge)$"); } catch { settings = ""; }
                if (settings.Length == 0) { try { Text("config", "--local", "--unset-all", "filter." + driver + ".required"); } catch { } }
            }
            foreach (var path in managedHooks) File.Delete(path);
            foreach (var original in originals) {
                if (original.PointerBackup != null) File.Copy(original.Restored, original.Path, true);
                var oid = Text("hash-object", "-w", "--no-filters", original.Restored);
                Text("update-index", "--add", "--cacheinfo", original.Mode, oid, Path.GetRelativePath(actual, original.Path).Replace('\\', '/'));
            }
            foreach (var folder in storage) {
                var destination = Path.Combine(metadata, Path.GetFileName(folder));
                moved.Add((folder, destination)); MoveUninstallStorage(folder, destination);
            }
            if (File.Exists(chunkIgnore)) File.Delete(chunkIgnore);
            foreach (var entry in entries.Where(e => e.Path == ".chunkignore" || e.Path.StartsWith(".gitchunk/", StringComparison.Ordinal) || e.Path.StartsWith(".resourcesplitter/", StringComparison.Ordinal)))
                Text("update-index", "--force-remove", "--", entry.Path);
            foreach (var path in attributes.Where(p => p != infoAttributes).Append(ignore)) {
                var relative = Path.GetRelativePath(actual, path).Replace('\\', '/');
                if (File.Exists(path)) Text("add", "--", relative);
            }
            foreach (var asset in assets) {
                var oid = Text("rev-parse", ":" + asset.Path);
                if (long.Parse(Text("cat-file", "-s", oid)) != pointers[asset.Oid].Size || Hash(Git(null, "cat-file", "blob", oid)) != pointers[asset.Oid].Sha256)
                    throw new Exception("Uninstall original verification failed: " + asset.Path);
            }
            Console.WriteLine("Git Chunk uninstalled from this repository. Originals verified and changes staged; no commit or push was made.");
            Console.WriteLine("Local recovery backup: " + metadata);
            Console.WriteLine("Historical chunked commits require reinstalling Git Chunk. Global commands and other repositories were preserved.");
        }
        catch {
            foreach (var item in moved.AsEnumerable().Reverse()) if (Directory.Exists(item.Backup)) MoveUninstallStorage(item.Backup, item.Path);
            foreach (var original in originals) if (original.PointerBackup != null) File.Copy(original.PointerBackup, original.Path, true);
            foreach (var item in snapshots) { if (item.Exists) File.Copy(item.Backup, item.Path, true); else if (File.Exists(item.Path)) File.Delete(item.Path); }
            Console.Error.WriteLine("Uninstall failed; index, settings, managed hooks and pointer working files restored. Backup: " + metadata);
            throw;
        }
    }
}
